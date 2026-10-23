namespace Sessions.Repo.Infrastructure.Requests
{
    public abstract class SessionKeyRepoRequest
    {
        public string ApplicationName { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime Now { get; set; }
    }
}
