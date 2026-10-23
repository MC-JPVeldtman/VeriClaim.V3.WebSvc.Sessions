using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Web;
using System.Web.SessionState;

namespace VeriClaim.SessionState.Client
{
    /// <summary>
    /// ASP.NET session-state provider backed by the VeriClaim Sessions API. Registered in web.config, so existing
    /// Session["..."] code keeps working unchanged. Use with cookieless="UseUri" for one session per browser tab.
    /// </summary>
    /// <remarks>
    /// Session_End in Global.asax is not raised: ASP.NET only raises it for InProc sessions.
    /// </remarks>
    public sealed class SessionServiceStateStore : SessionStateStoreProviderBase
    {
        private SessionServiceClient _client;
        private BrowserBinding _browserBinding;
        private bool _compressionEnabled;

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrEmpty(name)) name = nameof(SessionServiceStateStore);

            base.Initialize(name, config);

            var serviceUrl = Required(config, "serviceUrl");
            var applicationName = Required(config, "applicationName");
            var apiKey = !string.IsNullOrEmpty(config["apiKey"])
                ? config["apiKey"]
                : ConfigurationManager.AppSettings[Required(config, "apiKeyAppSetting")];
            if (string.IsNullOrEmpty(apiKey)) throw new ProviderException(name + ": the API key is empty.");

            _client = new SessionServiceClient(
                new Uri(serviceUrl.EndsWith("/") ? serviceUrl : serviceUrl + "/"),
                apiKey,
                applicationName,
                OptionalInt(config, "requestTimeoutMs", 10000),
                OptionalInt(config, "maxConnections", 100));
            _browserBinding = new BrowserBinding(
                OptionalBool(config, "bindToBrowser", true),
                Optional(config, "bindingCookieName", "VC_SessionBinding"),
                Optional(config, "bindingCookieSameSite", "Lax"));
            _compressionEnabled = OptionalBool(config, "compressionEnabled", true);
        }

        public override void InitializeRequest(HttpContext context)
        {
            // Issue the binding cookie now, while response headers can still be written.
            _browserBinding.GetKeyHash(context);
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return Acquire(false, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return Acquire(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            var data = SessionDataSerializer.Serialize(item, _compressionEnabled);

            // A false result means another request took over the lock (ASP.NET forced it after executionTimeout);
            // ASP.NET's own providers drop the write in that case, and so do we.
            _client.Save(id, lockId == null ? 0 : (int)lockId, data, item.Timeout, newItem, _browserBinding.GetKeyHash(context));
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            _client.Release(id, (int)lockId);
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            _client.Remove(id, (int)lockId);
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            _client.Touch(id, _browserBinding.GetKeyHash(context));
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            _client.CreateUninitialized(id, timeout, _browserBinding.GetKeyHash(context));
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override void Dispose()
        {
        }

        private SessionStateStoreData Acquire(bool exclusive, HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            locked = false;
            lockAge = TimeSpan.Zero;
            lockId = null;
            actions = SessionStateActions.None;

            var result = _client.Acquire(id, exclusive, _browserBinding.GetKeyHash(context));

            if (result.Status == AcquireSessionResult.StatusLocked)
            {
                locked = true;
                lockAge = TimeSpan.FromSeconds(result.LockAgeSeconds);
                lockId = result.LockId;
                return null;
            }

            if (result.Status != AcquireSessionResult.StatusFound)
            {
                // Missing, expired, or owned by another browser. With regenerateExpiredSessionId="true" ASP.NET
                // then issues a fresh id, so this tab starts a new, empty (logged-out) session.
                return null;
            }

            lockId = result.LockId;
            actions = result.IsUninitialized ? SessionStateActions.InitializeItem : SessionStateActions.None;

            return string.IsNullOrEmpty(result.Data)
                ? CreateNewStoreData(context, result.TimeoutMinutes)
                : SessionDataSerializer.Deserialize(context, Convert.FromBase64String(result.Data));
        }

        private string Required(NameValueCollection config, string key)
        {
            var value = config[key];
            if (string.IsNullOrEmpty(value)) throw new ProviderException(Name + ": the '" + key + "' attribute is required.");

            return value;
        }

        private static string Optional(NameValueCollection config, string key, string fallback)
        {
            return string.IsNullOrEmpty(config[key]) ? fallback : config[key];
        }

        private static int OptionalInt(NameValueCollection config, string key, int fallback)
        {
            int value;
            return int.TryParse(config[key], out value) ? value : fallback;
        }

        private static bool OptionalBool(NameValueCollection config, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(config[key], out value) ? value : fallback;
        }
    }
}
