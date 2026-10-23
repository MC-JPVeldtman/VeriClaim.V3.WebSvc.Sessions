namespace Sessions.Repo.Infrastructure.Requests
{
    public class InsertSessionRepoRequest : SessionKeyRepoRequest
    {
        public string BrowserKeyHash { get; set; } = string.Empty;
        public byte[]? Data { get; set; }
        public int TimeoutMinutes { get; set; }
        public bool Uninitialized { get; set; }
    }
}
