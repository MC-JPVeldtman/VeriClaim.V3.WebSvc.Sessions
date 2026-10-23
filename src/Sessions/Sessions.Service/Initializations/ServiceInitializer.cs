using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sessions.Repo.Infrastructure.Services;
using Sessions.Repo.Library.Configuration;
using Sessions.Repo.Library.Services;
using Sessions.Service.Configuration;
using Sessions.Service.Infrastructure.Configuration;
using Sessions.Service.Infrastructure.Services;
using Sessions.Service.Library.Services;
using VeriClaim.Logging.Library.Services;

namespace Sessions.Service.Initializations
{
    public static class ServiceInitializer
    {
        public static IServiceCollection RegisterApplicationServices(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<MongoOptions>(config.GetSection("Mongo"));
            services.Configure<SessionStateOptions>(config.GetSection("Sessions"));
            services.Configure<ApiKeyOptions>(config.GetSection("SessionApi"));

            services.AddSingleton<VeriClaim.Logging.Infrastructure.Services.ILogger, FileLogger>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IMongoClient>(provider => new MongoClient(provider.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
            services.AddSingleton<ISessionRepoService, SessionRepoService>();
            services.AddSingleton<ISessionStateService, SessionStateService>();

            return services;
        }
    }
}
