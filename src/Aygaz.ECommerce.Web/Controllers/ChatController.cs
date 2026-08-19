using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Web.Configuration;
using Aygaz.ECommerce.Web.Models;
using Aygaz.ECommerce.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatSessionStore _sessionStore;
    private readonly ChatApiOptions _options;

    public ChatController(
        IChatSessionStore sessionStore,
        IOptions<ChatApiOptions> options)
    {
        _sessionStore = sessionStore;
        _options = options.Value;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> PostAsync(
        [FromBody] ChatRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(CreateError("Geçerli bir istek gövdesi gereklidir."));
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(CreateError("Mesaj boş olamaz."));
        }

        string message = request.Message.Trim();
        if (message.Length > _options.MaxMessageLength)
        {
            return BadRequest(CreateError(
                $"Mesaj en fazla {_options.MaxMessageLength} karakter olabilir."));
        }

        ChatSessionHandle session = await _sessionStore.GetOrCreateAsync(
            request.SessionId,
            cancellationToken);

        try
        {
            GuardedAgentResponse agentResponse = await session.GuardedAgent.AskDetailedAsync(
                message,
                cancellationToken);

            return Ok(new ChatResponse(
                agentResponse.Success,
                agentResponse.Message,
                agentResponse.Scope,
                session.SessionId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AgentException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateError(exception.Message));
        }
        catch (LocalLlmException exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateError(exception.Message));
        }
    }

    [HttpPost("chat/clear")]
    public async Task<ActionResult<ClearSessionResponse>> ClearAsync(
        [FromBody] ClearSessionRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest(CreateError("Geçerli bir oturum kimliği gereklidir."));
        }

        string sessionId = request.SessionId.Trim();
        _sessionStore.ClearSession(sessionId);

        ChatSessionHandle refreshedSession = await _sessionStore.GetOrCreateAsync(sessionId);
        return Ok(new ClearSessionResponse(true, refreshedSession.SessionId));
    }

    private static ApiErrorResponse CreateError(string message)
    {
        return new ApiErrorResponse(false, message);
    }
}
