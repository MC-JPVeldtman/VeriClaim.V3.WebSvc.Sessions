namespace Sessions.Service.Infrastructure.Requests
{
    public class RemoveSessionRequest : SessionKeyRequest
    {
        public int LockId { get; set; }
    }
}
