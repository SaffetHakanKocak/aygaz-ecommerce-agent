using Aygaz.AgentFramework.Configuration;
using Aygaz.AgentFramework.Resilience;
using Aygaz.ECommerce.SemanticKernel.Services;
using Aygaz.ECommerce.SemanticKernel.Web.Configuration;
using Aygaz.ECommerce.SemanticKernel.Web.Models;
using Aygaz.ECommerce.SemanticKernel.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.SemanticKernel.Web.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class ChatController : ControllerBase
{
    public const string AiProviderUnavailableErrorCode = "AI_PROVIDER_TEMPORARILY_UNAVAILABLE";
    public const string AiProviderUnavailableMessage =
        "Yapay zekâ servisi şu anda yoğun. Lütfen birkaç saniye sonra tekrar deneyin.";

    private readonly ISemanticKernelChatService _chatService;
    private readonly IChatSessionStore _sessionStore;
    private readonly ChatApiOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly SemanticKernelOptions _semanticKernelOptions;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ISemanticKernelChatService chatService,
        IChatSessionStore sessionStore,
        IOptions<ChatApiOptions> options,
        SemanticKernelOptions semanticKernelOptions,
        IWebHostEnvironment environment,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _sessionStore = sessionStore;
        _options = options.Value;
        _environment = environment;
        _semanticKernelOptions = semanticKernelOptions;
        _logger = logger;
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

        try
        {
            return await _sessionStore.ExecuteExclusiveAsync(
                sessionId,
                async ct =>
                {
                    var sessionHistory = _sessionStore.GetHistory(sessionId);

                    SemanticKernelChatResult result = await _chatService.ProcessAsync(
                        message,
                        sessionHistory,
                        ct);
                    _sessionStore.AppendTurn(sessionId, message, result.Message);
                    return Ok(ResponseSanitizer.ToChatResponse(
                        result,
                        sessionId,
                        includeDebug: _environment.IsDevelopment()));
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AiProviderTemporarilyUnavailableException ex)
        {
            ChatErrorLogger.Log(_logger, ex, sessionId, _semanticKernelOptions.Provider);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse(false, AiProviderUnavailableMessage, AiProviderUnavailableErrorCode));
        }
        catch (Exception ex)
        {
            ChatErrorLogger.Log(_logger, ex, sessionId, _semanticKernelOptions.Provider);
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
