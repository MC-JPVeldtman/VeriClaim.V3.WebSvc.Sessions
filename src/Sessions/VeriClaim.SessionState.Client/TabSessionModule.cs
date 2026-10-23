using System;
using System.Web;
using System.Web.SessionState;

namespace VeriClaim.SessionState.Client
{
    /// <summary>
    /// Keeps hard-coded root-relative links (e.g. JavaScript location = '/Page.aspx') inside the tab's session.
    /// ASP.NET only adds the cookieless /(S(id))/ segment to links it renders itself; any other link would arrive
    /// without an id and silently start a new, logged-out session. For pages that use session state, this module
    /// redirects such requests back under the id of the page that linked to them (302 for GET/HEAD, 307 otherwise
    /// so form posts keep their body). The browser-binding cookie still applies, so a forged Referer gains nothing.
    /// </summary>
    public sealed class TabSessionModule : IHttpModule
    {
        private static readonly Lazy<SessionIDManager> IdManager = new Lazy<SessionIDManager>(() =>
        {
            var manager = new SessionIDManager();
            manager.Initialize();
            return manager;
        });

        public void Init(HttpApplication application)
        {
            application.PostMapRequestHandler += OnPostMapRequestHandler;
        }

        public void Dispose()
        {
        }

        private static void OnPostMapRequestHandler(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;
            if (!(context.Handler is IRequiresSessionState)) return;
            if (IdManager.Value.GetSessionID(context) != null) return;

            string redirectUrl;
            if (!CookielessReferrerRecovery.TryRecover(context.Request.Url, context.Request.UrlReferrer, context.Request.ApplicationPath, out redirectUrl)) return;

            var method = context.Request.HttpMethod;
            var keepsMethod = !(string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase));

            context.Response.StatusCode = keepsMethod ? 307 : 302;
            context.Response.RedirectLocation = redirectUrl;
            context.ApplicationInstance.CompleteRequest();
        }
    }
}
