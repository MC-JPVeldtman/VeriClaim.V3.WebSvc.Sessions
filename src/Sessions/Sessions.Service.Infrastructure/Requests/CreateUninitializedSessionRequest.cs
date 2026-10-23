namespace Sessions.Service.Infrastructure.Requests
{
    public class CreateUninitializedSessionRequest : SessionKeyRequest
    {
        public int TimeoutMinutes { get; set; }
    }
}
