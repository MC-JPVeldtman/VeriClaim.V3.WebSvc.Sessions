namespace Sessions.Repo.Infrastructure.Requests
{
    public class RemoveSessionRepoRequest : SessionKeyRepoRequest
    {
        public int LockId { get; set; }
    }
}
