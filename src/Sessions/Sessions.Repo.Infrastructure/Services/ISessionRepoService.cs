using Sessions.Repo.Infrastructure.Models;
using Sessions.Repo.Infrastructure.Requests;

namespace Sessions.Repo.Infrastructure.Services
{
    public interface ISessionRepoService
    {
        /// <summary>Creates the TTL index that deletes expired sessions. Safe to call on every startup.</summary>
        Task EnsureIndexesAsync();

        /// <summary>
        /// Reads a live session owned by the given browser, sliding its expiry and (when exclusive) taking the lock.
        /// Returns the document as it was before the update, or null when it is missing, expired, owned by
        /// another browser, or (when exclusive) already locked.
        /// </summary>
        Task<SessionDocument?> AcquireAsync(AcquireSessionRepoRequest request);

        /// <summary>Reads a live session owned by the given browser without changing it.</summary>
        Task<SessionDocument?> GetAsync(AcquireSessionRepoRequest request);

        /// <summary>
        /// Writes a brand new session. Returns false when the id already belongs to a live session of another
        /// browser, or when <see cref="InsertSessionRepoRequest.Uninitialized"/> is set and any session already exists.
        /// </summary>
        Task<bool> InsertAsync(InsertSessionRepoRequest request);

        /// <summary>Saves data and releases the lock. Returns false when the caller no longer holds the lock.</summary>
        Task<bool> UpdateAsync(UpdateSessionRepoRequest request);

        Task ReleaseAsync(ReleaseSessionRepoRequest request);

        Task RemoveAsync(RemoveSessionRepoRequest request);

        Task TouchAsync(TouchSessionRepoRequest request);
    }
}
