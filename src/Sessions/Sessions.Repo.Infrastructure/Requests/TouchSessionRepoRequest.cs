namespace Sessions.Repo.Infrastructure.Requests
{
    public class TouchSessionRepoRequest : SessionKeyRepoRequest
    {
        public string BrowserKeyHash { get; set; } = string.Empty;
    }
}
