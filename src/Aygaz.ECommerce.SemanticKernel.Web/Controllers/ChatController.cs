using Aygaz.ECommerce.SemanticKernel.Services;
using Aygaz.ECommerce.SemanticKernel.Web.Configuration;
using Aygaz.ECommerce.SemanticKernel.Web.Models;
using Aygaz.ECommerce.SemanticKernel.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.SemanticKernel.Web.Controllers;

[ApiController]
[Route("api")]
public sealed class ChatController : ControllerBase
{
    private readonly ISemanticKernelChatService _chatService;
    private readonly IChatSessionStore _sessionStore;
    private readonly ChatApiOptions _options;
    private readonly IWebHostEnvironment _environment;

    public ChatController(
        ISemanticKernelChatService chatService,
        IChatSessionStore sessionStore,
        IOptions<ChatApiOptions> options,
        IWebHostEnvironment environment)
    {
        _chatService = chatService;
        _sessionStore = sessionStore;
        _options = options.Value;
        _environment = environment;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> PostAsync(
        [FromBody] ChatRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new ApiErrorResponse(false, "Geçerli bir istek gövdesi gereklidir."));
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ApiErrorResponse(false, "Mesaj boş olamaz."));
        }

        string message = request.Message.Trim();
        if (message.Length > _options.MaxMessageLength)
        {
            return BadRequest(new ApiErrorResponse(
                false,
                $"Mesaj en fazla {_options.MaxMessageLength} karakter olabilir."));
        }

        string sessionId = _sessionStore.ResolveSessionId(request.SessionId);
        var sessionHistory = _sessionStore.GetHistory(sessionId);

        try
        {
            SemanticKernelChatResult result = await _chatService.ProcessAsync(
                message,
                sessionHistory,
                cancellationToken);
            _sessionStore.AppendTurn(sessionId, message, result.Message);
            return Ok(ResponseSanitizer.ToChatResponse(
                result,
                sessionId,
                includeDebug: _environment.IsDevelopment()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse(false, "İstek işlenirken bir hata oluştu."));
        }
    }

    [HttpPost("chat/clear")]
    public ActionResult<ClearSessionResponse> ClearAsync([FromBody] ClearSessionRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest(new ApiErrorResponse(false, "Geçerli bir oturum kimliği gereklidir."));
        }

        string sessionId = request.SessionId.Trim();
        _sessionStore.ClearSession(sessionId);
        string refreshedSessionId = _sessionStore.ResolveSessionId(sessionId);
        return Ok(new ClearSessionResponse(true, refreshedSessionId));
    }
}
