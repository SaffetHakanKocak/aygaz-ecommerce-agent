using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Services;

public interface ISemanticKernelChatService
{
    Task<SemanticKernelChatResult> ProcessAsync(
        string userMessage,
        ChatHistory? conversationHistory = null,
        CancellationToken cancellationToken = default);
}
