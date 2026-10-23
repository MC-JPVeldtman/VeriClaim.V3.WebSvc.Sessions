namespace Sessions.Repo.Infrastructure.Requests
{
    public class AcquireSessionRepoRequest : SessionKeyRepoRequest
    {
        public bool Exclusive { get; set; }
        public string BrowserKeyHash { get; set; } = string.Empty;
    }
}
