using Sessions.Service.Infrastructure.Requests;
using Sessions.Service.Infrastructure.Responses;

namespace Sessions.Service.Infrastructure.Services
{
    /// <summary>
    /// Mirrors ASP.NET's SessionStateStoreProviderBase so a legacy website can keep using Session["x"] unchanged.
    /// </summary>
    public interface ISessionStateService
    {
        /// <summary>Returns an error message when the key or body is invalid, otherwise null.</summary>
        string? Validate(SessionKeyRequest request);

        Task<AcquireSessionResponse> AcquireAsync(AcquireSessionRequest request);
        Task<SaveSessionResponse> SaveAsync(SaveSessionRequest request);
        Task CreateUninitializedAsync(CreateUninitializedSessionRequest request);
        Task ReleaseAsync(ReleaseSessionRequest request);
        Task RemoveAsync(RemoveSessionRequest request);
        Task TouchAsync(TouchSessionRequest request);
    }
}
