using Microsoft.AspNetCore.Mvc;
using Sessions.Service.Infrastructure.Requests;
using Sessions.Service.Infrastructure.Responses;
using Sessions.Service.Infrastructure.Services;

namespace Sessions.Service.Controllers
{
    /// <summary>
    /// One endpoint per SessionStateStoreProviderBase operation. {applicationName} keeps websites that share
    /// this service apart; {sessionId} is the per-tab id ASP.NET puts in the URL.
    /// </summary>
    [ApiController]
    [Route("api/sessions/{applicationName}/{sessionId}")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionStateService _sessionStateService;

        public SessionsController(ISessionStateService sessionStateService)
        {
            _sessionStateService = sessionStateService;
        }

        [HttpPost("acquire")]
        public async Task<ActionResult<AcquireSessionResponse>> Acquire(string applicationName, string sessionId, AcquireSessionRequest request)
        {
            var error = Prepare(applicationName, sessionId, request);
            if (error != null) return BadRequest(error);

            return Ok(await _sessionStateService.AcquireAsync(request));
        }

        [HttpPut]
        public async Task<ActionResult<SaveSessionResponse>> Save(string applicationName, string sessionId, SaveSessionRequest request)
        {
            var error = Prepare(applicationName, sessionId, request);
            if (error != null) return BadRequest(error);

            return Ok(await _sessionStateService.SaveAsync(request));
        }

        [HttpPost("uninitialized")]
        public async Task<IActionResult> CreateUninitialized(string applicationName, string sessionId, CreateUninitializedSessionRequest request)
        {
            var error = Prepare(applicationName, sessionId, request);
            if (error != null) return BadRequest(error);

            await _sessionStateService.CreateUninitializedAsync(request);

            return NoContent();
        }

        [HttpPost("release")]
        public async Task<IActionResult> Release(string applicationName, string sessionId, ReleaseSessionRequest request)
        {
            var error = Prepare(applicationName, sessionId, request);
            if (error != null) return BadRequest(error);

            await _sessionStateService.ReleaseAsync(request);

            return NoContent();
        }

        [HttpDelete]
        public async Task<IActionResult> Remove(string applicationName, string sessionId, [FromQuery] int lockId)
        {
            var request = new RemoveSessionRequest { LockId = lockId };
            var error = Prepare(applicationName, sessionId, request);
            if (error != null) return BadRequest(error);

            await _sessionStateService.RemoveAsync(request);

            return NoContent();
        }

        [HttpPost("touch")]
        public async Task<IActionResult> Touch(string applicationName, string sessionId, TouchSessionRequest request)
        {
            var error = Prepare(applicationName, sessionId, request);
            if (error != null) return BadRequest(error);

            await _sessionStateService.TouchAsync(request);

            return NoContent();
        }

        private string? Prepare(string applicationName, string sessionId, SessionKeyRequest request)
        {
            request.ApplicationName = applicationName;
            request.SessionId = sessionId;

            return _sessionStateService.Validate(request);
        }
    }
}
