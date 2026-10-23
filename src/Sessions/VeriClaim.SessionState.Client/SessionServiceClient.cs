using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;

namespace VeriClaim.SessionState.Client
{
    /// <summary>
    /// Synchronous HTTP client for the Sessions API. Session-state providers are synchronous, and HttpWebRequest
    /// avoids the sync-over-async deadlocks HttpClient can cause under the ASP.NET synchronization context.
    /// </summary>
    internal sealed class SessionServiceClient
    {
        private readonly Uri _baseUri;
        private readonly string _apiKey;
        private readonly string _applicationName;
        private readonly int _timeoutMilliseconds;

        public SessionServiceClient(Uri baseUri, string apiKey, string applicationName, int timeoutMilliseconds, int maxConnections)
        {
            _baseUri = baseUri;
            _apiKey = apiKey;
            _applicationName = applicationName;
            _timeoutMilliseconds = timeoutMilliseconds;

            // Every page request makes at least two calls, so keep connections warm and skip the 100-continue round trip.
            var servicePoint = ServicePointManager.FindServicePoint(baseUri);
            servicePoint.ConnectionLimit = maxConnections;
            servicePoint.Expect100Continue = false;
            servicePoint.UseNagleAlgorithm = false;
        }

        public AcquireSessionResult Acquire(string sessionId, bool exclusive, string browserKeyHash)
        {
            return Send<AcquireSessionBody, AcquireSessionResult>("POST", sessionId, "/acquire",
                new AcquireSessionBody { Exclusive = exclusive, BrowserKeyHash = browserKeyHash });
        }

        public bool Save(string sessionId, int lockId, byte[] data, int timeoutMinutes, bool isNew, string browserKeyHash)
        {
            var body = new SaveSessionBody
            {
                LockId = lockId,
                Data = data == null ? null : Convert.ToBase64String(data),
                TimeoutMinutes = timeoutMinutes,
                IsNew = isNew,
                BrowserKeyHash = browserKeyHash,
            };

            return Send<SaveSessionBody, SaveSessionResult>("PUT", sessionId, string.Empty, body).Saved;
        }

        public void CreateUninitialized(string sessionId, int timeoutMinutes, string browserKeyHash)
        {
            Send<CreateUninitializedSessionBody, object>("POST", sessionId, "/uninitialized",
                new CreateUninitializedSessionBody { TimeoutMinutes = timeoutMinutes, BrowserKeyHash = browserKeyHash });
        }

        public void Release(string sessionId, int lockId)
        {
            Send<ReleaseSessionBody, object>("POST", sessionId, "/release", new ReleaseSessionBody { LockId = lockId });
        }

        public void Remove(string sessionId, int lockId)
        {
            Send<object, object>("DELETE", sessionId, "?lockId=" + lockId, null);
        }

        public void Touch(string sessionId, string browserKeyHash)
        {
            Send<TouchSessionBody, object>("POST", sessionId, "/touch", new TouchSessionBody { BrowserKeyHash = browserKeyHash });
        }

        private TResponse Send<TRequest, TResponse>(string method, string sessionId, string pathSuffix, TRequest body)
            where TResponse : class
        {
            var uri = new Uri(_baseUri, "api/sessions/" + Uri.EscapeDataString(_applicationName) + "/" + Uri.EscapeDataString(sessionId) + pathSuffix);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = method;
            request.Timeout = _timeoutMilliseconds;
            request.ReadWriteTimeout = _timeoutMilliseconds;
            request.Accept = "application/json";
            request.Headers["X-Api-Key"] = _apiKey;

            try
            {
                if (body != null)
                {
                    request.ContentType = "application/json";
                    using (var requestStream = request.GetRequestStream())
                    {
                        new DataContractJsonSerializer(typeof(TRequest)).WriteObject(requestStream, body);
                    }
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (typeof(TResponse) == typeof(object) || response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return null;
                    }

                    using (var responseStream = response.GetResponseStream())
                    {
                        return (TResponse)new DataContractJsonSerializer(typeof(TResponse)).ReadObject(responseStream);
                    }
                }
            }
            catch (WebException exception)
            {
                var status = exception.Response is HttpWebResponse errorResponse ? (int)errorResponse.StatusCode + " " + ReadBody(errorResponse) : exception.Status.ToString();
                throw new SessionServiceException("Sessions API " + method + " " + uri.AbsolutePath + " failed: " + status, exception);
            }
        }

        private static string ReadBody(HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
