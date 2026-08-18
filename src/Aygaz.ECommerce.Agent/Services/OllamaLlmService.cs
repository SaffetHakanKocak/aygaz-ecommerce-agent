using Aygaz.ECommerce.Agent.Models;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaLlmService : ILocalLlmService
{
    private const string SystemPrompt =
        "Sen Aygaz E-Commerce AI Agent projesinin geliştirme aşamasındaki yerel yapay zeka " +
        "asistanısın. Şimdilik yalnızca bağlantı testi amacıyla çalışıyorsun. Kısa, açık ve " +
        "Türkçe cevaplar ver.";

    private readonly IOllamaChatClient _chatClient;

    public OllamaLlmService(IOllamaChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<string> AskAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        OllamaChatMessage responseMessage = await _chatClient.ChatAsync(
            [
                new OllamaChatMessage("system", SystemPrompt),
                new OllamaChatMessage("user", userMessage)
            ],
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(responseMessage.Content))
        {
            throw new LocalLlmException(
                "Local LLM geçerli bir yanıt döndürmedi.",
                "Ollama yanıtında message.content alanı boş veya eksik.");
        }

        return responseMessage.Content.Trim();
    }
}
