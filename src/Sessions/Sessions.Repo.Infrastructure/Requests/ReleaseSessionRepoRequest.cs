namespace Sessions.Repo.Infrastructure.Requests
{
    public class ReleaseSessionRepoRequest : SessionKeyRepoRequest
    {
        public int LockId { get; set; }
    }
}
