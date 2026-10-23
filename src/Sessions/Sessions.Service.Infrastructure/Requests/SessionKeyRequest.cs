using System.Text.Json.Serialization;

namespace Sessions.Service.Infrastructure.Requests
{
    /// <summary>Base for every request; the controller fills the key from the route, never from the body.</summary>
    public abstract class SessionKeyRequest
    {
        [JsonIgnore]
        public string ApplicationName { get; set; } = string.Empty;

        [JsonIgnore]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>SHA-256 of the caller's browser-binding cookie. A session is only visible to the browser that created it.</summary>
        public string BrowserKeyHash { get; set; } = string.Empty;
    }
}
