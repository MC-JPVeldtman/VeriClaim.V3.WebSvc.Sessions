namespace Sessions.Repo.Infrastructure.Requests
{
    public class UpdateSessionRepoRequest : SessionKeyRepoRequest
    {
        public int LockId { get; set; }
        public byte[]? Data { get; set; }
        public int TimeoutMinutes { get; set; }
    }
}
