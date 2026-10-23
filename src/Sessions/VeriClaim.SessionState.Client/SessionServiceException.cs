using System;

namespace VeriClaim.SessionState.Client
{
    public sealed class SessionServiceException : Exception
    {
        public SessionServiceException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }
    }
}
