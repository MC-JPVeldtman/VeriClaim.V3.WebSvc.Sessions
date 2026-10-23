using MongoDB.Bson.Serialization.Attributes;

namespace Sessions.Repo.Infrastructure.Models
{
    /// <summary>
    /// One browser-tab session as stored in Mongo. Data is the opaque, already serialized
    /// session item collection sent by the calling website; this service never inspects it.
    /// </summary>
    public class SessionDocument
    {
        public const string ExpiresField = "expires";
        public const string TimeoutMinutesField = "timeoutMinutes";
        public const string LockedField = "locked";
        public const string LockedAtField = "lockedAt";
        public const string LockIdField = "lockId";
        public const string UninitializedField = "uninitialized";

        [BsonId]
        public string Id { get; set; } = string.Empty;

        [BsonElement("applicationName")]
        public string ApplicationName { get; set; } = string.Empty;

        [BsonElement("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [BsonElement("browserKeyHash")]
        public string BrowserKeyHash { get; set; } = string.Empty;

        [BsonElement("data")]
        public byte[]? Data { get; set; }

        [BsonElement("created")]
        public DateTime Created { get; set; }

        /// <summary>Mongo's TTL monitor deletes the document once this time has passed.</summary>
        [BsonElement(ExpiresField)]
        public DateTime Expires { get; set; }

        [BsonElement(TimeoutMinutesField)]
        public int TimeoutMinutes { get; set; }

        [BsonElement(LockedField)]
        public bool Locked { get; set; }

        [BsonElement(LockedAtField)]
        public DateTime? LockedAt { get; set; }

        [BsonElement(LockIdField)]
        public int LockId { get; set; }

        /// <summary>True when created by CreateUninitializedItem and not yet read by a request.</summary>
        [BsonElement(UninitializedField)]
        public bool Uninitialized { get; set; }

        public static string BuildId(string applicationName, string sessionId) => $"{applicationName}:{sessionId}";
    }
}
