using Sessions.Service.Infrastructure.Models;

namespace Sessions.Service.Infrastructure.Responses
{
    public class AcquireSessionResponse
    {
        public SessionStatus Status { get; set; }
        public int LockId { get; set; }
        public int LockAgeSeconds { get; set; }
        public int TimeoutMinutes { get; set; }
        public byte[]? Data { get; set; }

        /// <summary>True when the session was pre-created by the cookieless redirect and has never been read.</summary>
        public bool IsUninitialized { get; set; }
    }
}
