using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sessions.Repo.Infrastructure.Models;
using Sessions.Repo.Infrastructure.Requests;
using Sessions.Repo.Infrastructure.Services;
using Sessions.Service.Infrastructure.Configuration;
using Sessions.Service.Infrastructure.Models;
using Sessions.Service.Infrastructure.Requests;
using Sessions.Service.Infrastructure.Responses;
using Sessions.Service.Infrastructure.Services;

namespace Sessions.Service.Library.Services
{
    public partial class SessionStateService : ISessionStateService
    {
        private readonly ISessionRepoService _sessionRepoService;
        private readonly TimeProvider _timeProvider;
        private readonly SessionStateOptions _options;

        public SessionStateService(ISessionRepoService sessionRepoService, TimeProvider timeProvider, IOptions<SessionStateOptions> options)
        {
            _sessionRepoService = sessionRepoService;
            _timeProvider = timeProvider;
            _options = options.Value;
        }

        // ASP.NET session ids are 24 chars of [a-z0-5]; allow a little more so other id managers still fit.
        [GeneratedRegex("^[A-Za-z0-9_.-]{1,80}$")]
        private static partial Regex KeyPattern();

        [GeneratedRegex("^([0-9a-f]{64})?$")]
        private static partial Regex HashPattern();

        public string? Validate(SessionKeyRequest request)
        {
            if (!KeyPattern().IsMatch(request.ApplicationName)) return "Invalid application name.";
            if (!KeyPattern().IsMatch(request.SessionId)) return "Invalid session id.";
            if (!HashPattern().IsMatch(request.BrowserKeyHash)) return "browserKeyHash must be a lowercase hex SHA-256 or empty.";

            return request switch
            {
                SaveSessionRequest save when (save.Data?.Length ?? 0) > _options.MaxDataBytes => $"Session data exceeds {_options.MaxDataBytes} bytes.",
                SaveSessionRequest save => ValidateTimeout(save.TimeoutMinutes),
                CreateUninitializedSessionRequest create => ValidateTimeout(create.TimeoutMinutes),
                _ => null,
            };
        }

        public async Task<AcquireSessionResponse> AcquireAsync(AcquireSessionRequest request)
        {
            var now = UtcNow();
            var repoRequest = new AcquireSessionRepoRequest
            {
                ApplicationName = request.ApplicationName,
                SessionId = request.SessionId,
                BrowserKeyHash = request.BrowserKeyHash,
                Exclusive = request.Exclusive,
                Now = now,
            };

            var before = await _sessionRepoService.AcquireAsync(repoRequest);
            if (before != null)
            {
                return before.Locked && !request.Exclusive
                    ? LockedResponse(before, now)
                    : new AcquireSessionResponse
                    {
                        Status = SessionStatus.Found,
                        LockId = request.Exclusive ? before.LockId + 1 : before.LockId,
                        TimeoutMinutes = before.TimeoutMinutes,
                        Data = before.Data,
                        IsUninitialized = before.Uninitialized,
                    };
            }

            if (!request.Exclusive)
            {
                return new AcquireSessionResponse { Status = SessionStatus.NotFound };
            }

            // The exclusive lock was refused: either someone else holds it or the session does not exist (for this browser).
            var current = await _sessionRepoService.GetAsync(repoRequest);

            return current is { Locked: true }
                ? LockedResponse(current, now)
                : new AcquireSessionResponse { Status = SessionStatus.NotFound };
        }

        public async Task<SaveSessionResponse> SaveAsync(SaveSessionRequest request)
        {
            var now = UtcNow();
            var saved = request.IsNew
                ? await _sessionRepoService.InsertAsync(new InsertSessionRepoRequest
                {
                    ApplicationName = request.ApplicationName,
                    SessionId = request.SessionId,
                    BrowserKeyHash = request.BrowserKeyHash,
                    Data = request.Data,
                    TimeoutMinutes = request.TimeoutMinutes,
                    Now = now,
                })
                : await _sessionRepoService.UpdateAsync(new UpdateSessionRepoRequest
                {
                    ApplicationName = request.ApplicationName,
                    SessionId = request.SessionId,
                    LockId = request.LockId,
                    Data = request.Data,
                    TimeoutMinutes = request.TimeoutMinutes,
                    Now = now,
                });

            return new SaveSessionResponse { Saved = saved };
        }

        public async Task CreateUninitializedAsync(CreateUninitializedSessionRequest request)
        {
            // A false result means the id is already live; ASP.NET ignores that case too.
            await _sessionRepoService.InsertAsync(new InsertSessionRepoRequest
            {
                ApplicationName = request.ApplicationName,
                SessionId = request.SessionId,
                BrowserKeyHash = request.BrowserKeyHash,
                TimeoutMinutes = request.TimeoutMinutes,
                Uninitialized = true,
                Now = UtcNow(),
            });
        }

        public async Task ReleaseAsync(ReleaseSessionRequest request)
        {
            await _sessionRepoService.ReleaseAsync(new ReleaseSessionRepoRequest
            {
                ApplicationName = request.ApplicationName,
                SessionId = request.SessionId,
                LockId = request.LockId,
                Now = UtcNow(),
            });
        }

        public async Task RemoveAsync(RemoveSessionRequest request)
        {
            await _sessionRepoService.RemoveAsync(new RemoveSessionRepoRequest
            {
                ApplicationName = request.ApplicationName,
                SessionId = request.SessionId,
                LockId = request.LockId,
                Now = UtcNow(),
            });
        }

        public async Task TouchAsync(TouchSessionRequest request)
        {
            await _sessionRepoService.TouchAsync(new TouchSessionRepoRequest
            {
                ApplicationName = request.ApplicationName,
                SessionId = request.SessionId,
                BrowserKeyHash = request.BrowserKeyHash,
                Now = UtcNow(),
            });
        }

        private string? ValidateTimeout(int timeoutMinutes)
        {
            return timeoutMinutes < _options.MinTimeoutMinutes || timeoutMinutes > _options.MaxTimeoutMinutes
                ? $"timeoutMinutes must be between {_options.MinTimeoutMinutes} and {_options.MaxTimeoutMinutes}."
                : null;
        }

        private static AcquireSessionResponse LockedResponse(SessionDocument session, DateTime now)
        {
            return new AcquireSessionResponse
            {
                Status = SessionStatus.Locked,
                LockId = session.LockId,
                LockAgeSeconds = session.LockedAt.HasValue ? (int)Math.Max(0, (now - session.LockedAt.Value).TotalSeconds) : 0,
                TimeoutMinutes = session.TimeoutMinutes,
            };
        }

        // Mongo stores milliseconds, so trim here to keep comparisons exact.
        private DateTime UtcNow()
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        }
    }
}
