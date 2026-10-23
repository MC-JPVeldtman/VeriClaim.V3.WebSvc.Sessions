namespace Sessions.Service.Infrastructure.Requests
{
    public class AcquireSessionRequest : SessionKeyRequest
    {
        /// <summary>True for pages that write to Session (takes the lock); false for read-only pages.</summary>
        public bool Exclusive { get; set; }
    }
}
