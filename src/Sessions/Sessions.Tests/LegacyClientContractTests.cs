using VeriClaim.SessionState.Client;

namespace Sessions.Tests
{
    /// <summary>
    /// Drives the .NET Framework provider's HTTP client (DataContractJsonSerializer + HttpWebRequest) against the
    /// real API over a real socket, proving both sides agree on routes, JSON names, enums and base64 data.
    /// </summary>
    public class LegacyClientContractTests : IClassFixture<KestrelSessionsApiFactory>
    {
        private static readonly string Browser = new('c', 64);
        private readonly KestrelSessionsApiFactory _factory;

        public LegacyClientContractTests(KestrelSessionsApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void FullProviderLifecycle_RoundTripsOverHttp()
        {
            var client = CreateClient(SessionsApiFactory.ApiKey);
            var sessionId = Guid.NewGuid().ToString("N")[..24];
            var data = Enumerable.Range(0, 5000).Select(index => (byte)index).ToArray();

            client.CreateUninitialized(sessionId, 120, Browser);

            var first = client.Acquire(sessionId, true, Browser);
            Assert.Equal(AcquireSessionResult.StatusFound, first.Status);
            Assert.True(first.IsUninitialized);
            Assert.Null(first.Data);

            Assert.True(client.Save(sessionId, first.LockId, data, 120, false, Browser));

            var second = client.Acquire(sessionId, true, Browser);
            Assert.Equal(AcquireSessionResult.StatusFound, second.Status);
            Assert.Equal(data, Convert.FromBase64String(second.Data));

            var blocked = client.Acquire(sessionId, true, Browser);
            Assert.Equal(AcquireSessionResult.StatusLocked, blocked.Status);
            Assert.Equal(second.LockId, blocked.LockId);

            client.Release(sessionId, second.LockId);
            client.Touch(sessionId, Browser);

            var third = client.Acquire(sessionId, true, Browser);
            client.Remove(sessionId, third.LockId);
            Assert.Equal("NotFound", client.Acquire(sessionId, false, Browser).Status);
        }

        [Fact]
        public void WrongApiKey_ThrowsSessionServiceException()
        {
            var client = CreateClient("wrong-key");

            var exception = Assert.Throws<SessionServiceException>(() => client.Acquire("abc", false, Browser));
            Assert.Contains("401", exception.Message);
        }

        private SessionServiceClient CreateClient(string apiKey)
        {
            return new SessionServiceClient(_factory.BaseAddress, apiKey, "VeriClaimWebsite", 10000, 10);
        }
    }
}
