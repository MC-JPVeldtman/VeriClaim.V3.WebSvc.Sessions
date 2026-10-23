using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Driver;
using Sessions.Service.Infrastructure.Models;
using Sessions.Service.Infrastructure.Responses;

namespace Sessions.Tests
{
    public class SessionsApiTests : IClassFixture<SessionsApiFactory>
    {
        private const string App = "VeriClaimWebsite";
        private static readonly string BrowserA = new('a', 64);
        private static readonly string BrowserB = new('b', 64);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

        private readonly SessionsApiFactory _factory;
        private readonly HttpClient _client;

        public SessionsApiTests(SessionsApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateAuthorizedClient();
        }

        [Fact]
        public async Task Acquire_UnknownSession_ReturnsNotFound()
        {
            var response = await AcquireAsync(NewSessionId(), exclusive: true, BrowserA);

            Assert.Equal(SessionStatus.NotFound, response.Status);
        }

        [Fact]
        public async Task CookielessFlow_CreatesUninitializedSession_ThenSavesAndReadsData()
        {
            var sessionId = NewSessionId();
            await CreateUninitializedAsync(sessionId, BrowserA);

            var first = await AcquireAsync(sessionId, exclusive: true, BrowserA);
            Assert.Equal(SessionStatus.Found, first.Status);
            Assert.True(first.IsUninitialized);
            Assert.Equal(1, first.LockId);

            Assert.True(await SaveAsync(sessionId, first.LockId, [1, 2, 3], isNew: false, BrowserA));

            var second = await AcquireAsync(sessionId, exclusive: false, BrowserA);
            Assert.Equal(SessionStatus.Found, second.Status);
            Assert.False(second.IsUninitialized);
            Assert.Equal(new byte[] { 1, 2, 3 }, second.Data);
        }

        [Fact]
        public async Task ExclusiveLock_BlocksOtherRequests_UntilReleased()
        {
            var sessionId = await CreateSavedSessionAsync(BrowserA);
            var holder = await AcquireAsync(sessionId, exclusive: true, BrowserA);

            _factory.Clock.Advance(TimeSpan.FromSeconds(5));
            var writer = await AcquireAsync(sessionId, exclusive: true, BrowserA);
            var reader = await AcquireAsync(sessionId, exclusive: false, BrowserA);

            Assert.Equal(SessionStatus.Locked, writer.Status);
            Assert.Equal(holder.LockId, writer.LockId);
            Assert.Equal(5, writer.LockAgeSeconds);
            Assert.Equal(SessionStatus.Locked, reader.Status);

            await PostAsync(sessionId, "release", new { lockId = holder.LockId });

            var next = await AcquireAsync(sessionId, exclusive: true, BrowserA);
            Assert.Equal(SessionStatus.Found, next.Status);
            Assert.Equal(holder.LockId + 1, next.LockId);
        }

        [Fact]
        public async Task Save_WithLockThatWasTakenOver_IsRejected()
        {
            var sessionId = await CreateSavedSessionAsync(BrowserA);
            var stale = await AcquireAsync(sessionId, exclusive: true, BrowserA);
            await PostAsync(sessionId, "release", new { lockId = stale.LockId });
            await AcquireAsync(sessionId, exclusive: true, BrowserA);

            Assert.False(await SaveAsync(sessionId, stale.LockId, [9], isNew: false, BrowserA));
        }

        [Fact]
        public async Task SessionFromAnotherBrowser_IsInvisible_AndCannotBeOverwritten()
        {
            var sessionId = await CreateSavedSessionAsync(BrowserA);

            Assert.Equal(SessionStatus.NotFound, (await AcquireAsync(sessionId, exclusive: true, BrowserB)).Status);
            Assert.Equal(SessionStatus.NotFound, (await AcquireAsync(sessionId, exclusive: false, BrowserB)).Status);
            Assert.False(await SaveAsync(sessionId, 0, [6, 6, 6], isNew: true, BrowserB));

            var owner = await AcquireAsync(sessionId, exclusive: false, BrowserA);
            Assert.Equal(new byte[] { 7 }, owner.Data);
        }

        [Fact]
        public async Task SeparateTabs_HaveIndependentSessions()
        {
            var tabOne = NewSessionId();
            var tabTwo = NewSessionId();
            Assert.True(await SaveAsync(tabOne, 0, [1], isNew: true, BrowserA));
            Assert.True(await SaveAsync(tabTwo, 0, [2], isNew: true, BrowserA));

            Assert.Equal(new byte[] { 1 }, (await AcquireAsync(tabOne, exclusive: false, BrowserA)).Data);
            Assert.Equal(new byte[] { 2 }, (await AcquireAsync(tabTwo, exclusive: false, BrowserA)).Data);
        }

        [Fact]
        public async Task Remove_DeletesTheSession()
        {
            var sessionId = await CreateSavedSessionAsync(BrowserA);
            var held = await AcquireAsync(sessionId, exclusive: true, BrowserA);

            var response = await _client.DeleteAsync($"api/sessions/{App}/{sessionId}?lockId={held.LockId}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(SessionStatus.NotFound, (await AcquireAsync(sessionId, exclusive: false, BrowserA)).Status);
            Assert.Equal(0, await CountDocumentsAsync(sessionId));
        }

        [Fact]
        public async Task Session_SlidesOnUse_AndExpiresWhenIdle()
        {
            var sessionId = NewSessionId();
            Assert.True(await SaveAsync(sessionId, 0, [1], isNew: true, BrowserA, timeoutMinutes: 20));

            _factory.Clock.Advance(TimeSpan.FromMinutes(15));
            await PostAsync(sessionId, "touch", new { browserKeyHash = BrowserA });

            _factory.Clock.Advance(TimeSpan.FromMinutes(15));
            Assert.Equal(SessionStatus.Found, (await AcquireAsync(sessionId, exclusive: false, BrowserA)).Status);

            _factory.Clock.Advance(TimeSpan.FromMinutes(21));
            Assert.Equal(SessionStatus.NotFound, (await AcquireAsync(sessionId, exclusive: false, BrowserA)).Status);
        }

        [Fact]
        public async Task ExpiredSessionId_CanBeReusedByAnotherBrowser()
        {
            var sessionId = NewSessionId();
            Assert.True(await SaveAsync(sessionId, 0, [1], isNew: true, BrowserA, timeoutMinutes: 1));
            _factory.Clock.Advance(TimeSpan.FromMinutes(2));

            Assert.True(await SaveAsync(sessionId, 0, [2], isNew: true, BrowserB));
            Assert.Equal(new byte[] { 2 }, (await AcquireAsync(sessionId, exclusive: false, BrowserB)).Data);
        }

        [Fact]
        public async Task TtlIndex_DeletesDocumentsWhenTheyExpire()
        {
            await _client.GetAsync("/health");
            var cursor = await _factory.Database.GetCollection<BsonDocument>("Sessions").Indexes.ListAsync();
            var indexes = await IAsyncCursorExtensions.ToListAsync(cursor);

            var ttl = Assert.Single(indexes, index => index["name"] == "ttl_expires");
            Assert.Equal(0, ttl["expireAfterSeconds"].ToInt32());
            Assert.Equal(new BsonDocument("expires", 1), ttl["key"].AsBsonDocument);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("wrong-key")]
        public async Task Requests_WithoutAValidApiKey_AreRejected(string? apiKey)
        {
            var client = _factory.CreateClient();
            if (apiKey != null) client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var response = await client.PostAsJsonAsync($"api/sessions/{App}/{NewSessionId()}/acquire", new { exclusive = true, browserKeyHash = BrowserA });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("bad id!", "")]
        [InlineData("validid", "NOT-A-HASH")]
        public async Task InvalidKeys_AreRejected(string sessionId, string browserKeyHash)
        {
            var response = await _client.PostAsJsonAsync($"api/sessions/{App}/{Uri.EscapeDataString(sessionId)}/acquire", new { exclusive = true, browserKeyHash });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Save_WithTimeoutOutOfRange_IsRejected()
        {
            var response = await _client.PutAsJsonAsync($"api/sessions/{App}/{NewSessionId()}", new { lockId = 0, timeoutMinutes = 0, isNew = true, browserKeyHash = BrowserA });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private static string NewSessionId() => Guid.NewGuid().ToString("N")[..24];

        private async Task<string> CreateSavedSessionAsync(string browserKeyHash)
        {
            var sessionId = NewSessionId();
            Assert.True(await SaveAsync(sessionId, 0, [7], isNew: true, browserKeyHash));

            return sessionId;
        }

        private async Task<AcquireSessionResponse> AcquireAsync(string sessionId, bool exclusive, string browserKeyHash)
        {
            var response = await _client.PostAsJsonAsync($"api/sessions/{App}/{sessionId}/acquire", new { exclusive, browserKeyHash });
            response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync<AcquireSessionResponse>(Json))!;
        }

        private async Task<bool> SaveAsync(string sessionId, int lockId, byte[] data, bool isNew, string browserKeyHash, int timeoutMinutes = 120)
        {
            var response = await _client.PutAsJsonAsync($"api/sessions/{App}/{sessionId}", new { lockId, data, timeoutMinutes, isNew, browserKeyHash });
            response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync<SaveSessionResponse>(Json))!.Saved;
        }

        private async Task CreateUninitializedAsync(string sessionId, string browserKeyHash)
        {
            await PostAsync(sessionId, "uninitialized", new { timeoutMinutes = 120, browserKeyHash });
        }

        private async Task PostAsync(string sessionId, string action, object body)
        {
            var response = await _client.PostAsJsonAsync($"api/sessions/{App}/{sessionId}/{action}", body);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        private async Task<long> CountDocumentsAsync(string sessionId)
        {
            return await _factory.Database.GetCollection<BsonDocument>("Sessions").CountDocumentsAsync(new BsonDocument("sessionId", sessionId));
        }
    }
}
