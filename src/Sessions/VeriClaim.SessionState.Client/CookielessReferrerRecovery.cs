using System;
using System.Text.RegularExpressions;

namespace VeriClaim.SessionState.Client
{
    /// <summary>
    /// Pure URL logic for <see cref="TabSessionModule"/>. Kept free of System.Web so it can be unit tested.
    /// </summary>
    internal static class CookielessReferrerRecovery
    {
        // ASP.NET's SessionIDManager ids: 24 characters of [a-z0-5].
        private static readonly Regex SessionSegment = new Regex(@"^/\(S\(([a-z0-5]{24})\)\)(/|$)", RegexOptions.Compiled);

        /// <summary>
        /// When a request without a session id in its URL came from a same-origin page that had one, returns the
        /// request URL with that page's id inserted, e.g. /Diary.aspx?x=1 from /(S(abc...))/Main.aspx gives
        /// /(S(abc...))/Diary.aspx?x=1.
        /// </summary>
        public static bool TryRecover(Uri requestUrl, Uri referrer, string applicationPath, out string redirectUrl)
        {
            redirectUrl = null;
            if (requestUrl == null || referrer == null || !referrer.IsAbsoluteUri) return false;
            if (Uri.Compare(requestUrl, referrer, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0) return false;

            var appPath = (applicationPath ?? "/").TrimEnd('/');
            var referrerPath = referrer.AbsolutePath;
            if (!referrerPath.StartsWith(appPath + "/", StringComparison.OrdinalIgnoreCase)) return false;

            var match = SessionSegment.Match(referrerPath.Substring(appPath.Length));
            if (!match.Success) return false;

            var requestPathAndQuery = requestUrl.PathAndQuery;
            if (!requestPathAndQuery.StartsWith(appPath + "/", StringComparison.OrdinalIgnoreCase)) return false;

            redirectUrl = appPath + "/(S(" + match.Groups[1].Value + "))" + requestPathAndQuery.Substring(appPath.Length);
            return true;
        }
    }
}
