using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace VeriClaim.SessionState.Client
{
    /// <summary>
    /// Ties every session to the browser that created it. The session id lives in the URL (one per tab), so on its
    /// own a copied or leaked link would hand the session to whoever opened it. A random, HttpOnly, browser-session
    /// cookie is shared by all tabs of one browser; only its SHA-256 is sent to the API, and the API hides sessions
    /// whose stored hash differs. A link opened in another browser therefore just starts a new, empty session.
    /// </summary>
    internal sealed class BrowserBinding
    {
        private const string ContextItemKey = "VeriClaim.SessionState.BrowserKeyHash";
        private static readonly Regex KeyPattern = new Regex("^[0-9a-f]{64}$", RegexOptions.Compiled);

        private readonly bool _enabled;
        private readonly string _cookieName;
        private readonly string _sameSite;

        public BrowserBinding(bool enabled, string cookieName, string sameSite)
        {
            _enabled = enabled;
            _cookieName = cookieName;
            _sameSite = sameSite;
        }

        /// <summary>Returns the hash for this request, issuing the cookie first if the browser does not have one yet.</summary>
        public string GetKeyHash(HttpContext context)
        {
            if (!_enabled)
            {
                return string.Empty;
            }

            var cachedHash = context.Items[ContextItemKey] as string;
            if (cachedHash != null)
            {
                return cachedHash;
            }

            var cookie = context.Request.Cookies[_cookieName];
            var key = cookie == null ? null : cookie.Value;
            if (key == null || !KeyPattern.IsMatch(key))
            {
                key = CreateKey();
                IssueCookie(context, key);
            }

            var hash = Sha256Hex(key);
            context.Items[ContextItemKey] = hash;

            return hash;
        }

        private void IssueCookie(HttpContext context, string key)
        {
            // HttpCookie.SameSite only exists from .NET 4.7.2, so write the header ourselves. No Expires: the cookie
            // dies with the browser, which orphans its sessions and lets the Mongo TTL index delete them.
            var isSecure = context.Request.IsSecureConnection || string.Equals(_sameSite, "None", StringComparison.OrdinalIgnoreCase);
            var header = _cookieName + "=" + key + "; path=/; HttpOnly; SameSite=" + _sameSite + (isSecure ? "; Secure" : string.Empty);

            context.Response.AppendHeader("Set-Cookie", header);
        }

        private static string CreateKey()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return ToHex(bytes);
        }

        private static string Sha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(Encoding.ASCII.GetBytes(value)));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
