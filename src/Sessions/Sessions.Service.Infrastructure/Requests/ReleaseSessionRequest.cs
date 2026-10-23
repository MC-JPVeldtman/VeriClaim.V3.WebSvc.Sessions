namespace Sessions.Service.Infrastructure.Requests
{
    public class ReleaseSessionRequest : SessionKeyRequest
    {
        public int LockId { get; set; }
    }
}
