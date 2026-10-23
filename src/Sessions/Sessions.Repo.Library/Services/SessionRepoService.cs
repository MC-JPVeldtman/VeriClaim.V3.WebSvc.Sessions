using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sessions.Repo.Infrastructure.Models;
using Sessions.Repo.Infrastructure.Requests;
using Sessions.Repo.Infrastructure.Services;
using Sessions.Repo.Library.Configuration;

namespace Sessions.Repo.Library.Services
{
    public class SessionRepoService : ISessionRepoService
    {
        private readonly IMongoCollection<SessionDocument> _sessions;
        private static readonly FilterDefinitionBuilder<SessionDocument> Filter = Builders<SessionDocument>.Filter;

        public SessionRepoService(IMongoClient client, IOptions<MongoOptions> options)
        {
            _sessions = client
                .GetDatabase(options.Value.DatabaseName)
                .GetCollection<SessionDocument>(options.Value.CollectionName);
        }

        public async Task EnsureIndexesAsync()
        {
            // expireAfterSeconds = 0 deletes each document as soon as its own Expires date passes.
            // Mongo's TTL monitor runs about once a minute, so reads also filter on Expires.
            var ttlIndex = new CreateIndexModel<SessionDocument>(
                Builders<SessionDocument>.IndexKeys.Ascending(session => session.Expires),
                new CreateIndexOptions { Name = "ttl_expires", ExpireAfter = TimeSpan.Zero });

            await _sessions.Indexes.CreateOneAsync(ttlIndex);
        }

        public async Task<SessionDocument?> AcquireAsync(AcquireSessionRepoRequest request)
        {
            var filter = LiveSessionFilter(request.ApplicationName, request.SessionId, request.BrowserKeyHash, request.Now);
            var fieldsToSet = new BsonDocument(SessionDocument.ExpiresField, SlidingExpiry(request.Now));

            if (request.Exclusive)
            {
                filter &= Filter.Eq(session => session.Locked, false);
                fieldsToSet.Add(SessionDocument.LockedField, true);
                fieldsToSet.Add(SessionDocument.LockedAtField, request.Now);
                fieldsToSet.Add(SessionDocument.LockIdField, new BsonDocument("$add", new BsonArray { "$" + SessionDocument.LockIdField, 1 }));
                fieldsToSet.Add(SessionDocument.UninitializedField, false);
            }
            else
            {
                // A read-only request must not consume the "uninitialized" flag while another request holds the lock.
                fieldsToSet.Add(SessionDocument.UninitializedField, new BsonDocument("$cond", new BsonArray
                {
                    "$" + SessionDocument.LockedField,
                    "$" + SessionDocument.UninitializedField,
                    false,
                }));
            }

            return await _sessions.FindOneAndUpdateAsync(
                filter,
                PipelineSet(fieldsToSet),
                new FindOneAndUpdateOptions<SessionDocument> { ReturnDocument = ReturnDocument.Before });
        }

        public async Task<SessionDocument?> GetAsync(AcquireSessionRepoRequest request)
        {
            var filter = LiveSessionFilter(request.ApplicationName, request.SessionId, request.BrowserKeyHash, request.Now);

            return await _sessions.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<bool> InsertAsync(InsertSessionRepoRequest request)
        {
            var id = SessionDocument.BuildId(request.ApplicationName, request.SessionId);

            // Only an expired document may be replaced, or (for a real save) one this same browser already owns.
            // Otherwise the upsert collides with the existing _id and we report that nothing was written.
            var replaceableFilter = Filter.Lte(session => session.Expires, request.Now);
            if (!request.Uninitialized)
            {
                replaceableFilter |= Filter.Eq(session => session.BrowserKeyHash, request.BrowserKeyHash);
            }

            var session = new SessionDocument
            {
                Id = id,
                ApplicationName = request.ApplicationName,
                SessionId = request.SessionId,
                BrowserKeyHash = request.BrowserKeyHash,
                Data = request.Data,
                Created = request.Now,
                Expires = request.Now.AddMinutes(request.TimeoutMinutes),
                TimeoutMinutes = request.TimeoutMinutes,
                Uninitialized = request.Uninitialized,
            };

            try
            {
                await _sessions.ReplaceOneAsync(
                    Filter.Eq(existing => existing.Id, id) & replaceableFilter,
                    session,
                    new ReplaceOptions { IsUpsert = true });

                return true;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<bool> UpdateAsync(UpdateSessionRepoRequest request)
        {
            var update = Builders<SessionDocument>.Update
                .Set(session => session.Data, request.Data)
                .Set(session => session.TimeoutMinutes, request.TimeoutMinutes)
                .Set(session => session.Expires, request.Now.AddMinutes(request.TimeoutMinutes))
                .Set(session => session.Locked, false)
                .Set(session => session.LockedAt, null)
                .Set(session => session.Uninitialized, false);

            var result = await _sessions.UpdateOneAsync(LockHolderFilter(request.ApplicationName, request.SessionId, request.LockId), update);

            return result.MatchedCount > 0;
        }

        public async Task ReleaseAsync(ReleaseSessionRepoRequest request)
        {
            var fieldsToSet = new BsonDocument
            {
                { SessionDocument.LockedField, false },
                { SessionDocument.LockedAtField, BsonNull.Value },
                { SessionDocument.ExpiresField, SlidingExpiry(request.Now) },
            };

            await _sessions.UpdateOneAsync(LockHolderFilter(request.ApplicationName, request.SessionId, request.LockId), PipelineSet(fieldsToSet));
        }

        public async Task RemoveAsync(RemoveSessionRepoRequest request)
        {
            await _sessions.DeleteOneAsync(LockHolderFilter(request.ApplicationName, request.SessionId, request.LockId));
        }

        public async Task TouchAsync(TouchSessionRepoRequest request)
        {
            var filter = LiveSessionFilter(request.ApplicationName, request.SessionId, request.BrowserKeyHash, request.Now);

            await _sessions.UpdateOneAsync(filter, PipelineSet(new BsonDocument(SessionDocument.ExpiresField, SlidingExpiry(request.Now))));
        }

        private static FilterDefinition<SessionDocument> LiveSessionFilter(string applicationName, string sessionId, string browserKeyHash, DateTime now)
        {
            return Filter.Eq(session => session.Id, SessionDocument.BuildId(applicationName, sessionId))
                & Filter.Eq(session => session.BrowserKeyHash, browserKeyHash)
                & Filter.Gt(session => session.Expires, now);
        }

        private static FilterDefinition<SessionDocument> LockHolderFilter(string applicationName, string sessionId, int lockId)
        {
            return Filter.Eq(session => session.Id, SessionDocument.BuildId(applicationName, sessionId))
                & Filter.Eq(session => session.LockId, lockId);
        }

        /// <summary>now + the document's own timeoutMinutes, evaluated inside Mongo so it needs no extra read.</summary>
        private static BsonDocument SlidingExpiry(DateTime now)
        {
            return new BsonDocument("$add", new BsonArray
            {
                now,
                new BsonDocument("$multiply", new BsonArray { "$" + SessionDocument.TimeoutMinutesField, 60_000 }),
            });
        }

        private static UpdateDefinition<SessionDocument> PipelineSet(BsonDocument fieldsToSet)
        {
            var pipeline = PipelineDefinition<SessionDocument, SessionDocument>.Create(new BsonDocument("$set", fieldsToSet));

            return Builders<SessionDocument>.Update.Pipeline(pipeline);
        }
    }
}
