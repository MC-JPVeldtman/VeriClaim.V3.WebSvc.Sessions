using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Sessions.Service.Configuration;

namespace Sessions.Service.Filters
{
    /// <summary>
    /// Server-to-server authentication: only the websites holding a configured key may read or write sessions.
    /// With no keys configured every request is refused.
    /// </summary>
    public class ApiKeyAuthorizationFilter : IAuthorizationFilter
    {
        private readonly byte[][] _apiKeys;

        public ApiKeyAuthorizationFilter(IOptions<ApiKeyOptions> options)
        {
            _apiKeys = options.Value.ApiKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => Encoding.UTF8.GetBytes(key))
                .ToArray();
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var suppliedKey = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers[ApiKeyOptions.HeaderName].ToString());
            var isAuthorized = suppliedKey.Length > 0 && _apiKeys.Any(key => CryptographicOperations.FixedTimeEquals(key, suppliedKey));

            if (!isAuthorized)
            {
                context.Result = new UnauthorizedResult();
            }
        }
    }
}
