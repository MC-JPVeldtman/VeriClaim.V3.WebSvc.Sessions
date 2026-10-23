using EphemeralMongo;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;

namespace Sessions.Tests
{
    /// <summary>Runs the real API against a throwaway mongod with a controllable clock.</summary>
    public class SessionsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string ApiKey = "test-api-key";
        public const string DatabaseName = "SessionsTests";

        private IMongoRunner? _mongoRunner;

        // Start at the real time: Mongo's TTL monitor uses the server clock, so dates in the past would be purged.
        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

        public IMongoDatabase Database => new MongoClient(_mongoRunner!.ConnectionString).GetDatabase(DatabaseName);

        public virtual async Task InitializeAsync()
        {
            _mongoRunner = await MongoRunner.RunAsync(new MongoRunnerOptions { UseSingleNodeReplicaSet = false });
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Mongo:ConnectionString", _mongoRunner!.ConnectionString);
            builder.UseSetting("Mongo:DatabaseName", DatabaseName);
            builder.UseSetting("SessionApi:ApiKeys:0", ApiKey);
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
        }

        public HttpClient CreateAuthorizedClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

            return client;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await DisposeAsync();
            _mongoRunner?.Dispose();
        }
    }

    /// <summary>Same API on a real socket, for clients that cannot use the in-memory test server.</summary>
    public class KestrelSessionsApiFactory : SessionsApiFactory
    {
        public Uri BaseAddress { get; private set; } = null!;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            UseKestrel(0);
            StartServer();

            var address = Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            BaseAddress = new Uri(address + "/");
        }
    }
}
