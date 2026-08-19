using System.Collections.ObjectModel;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Rag;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class DocumentToolExecutor : IAgentToolModule
{
    public const string SearchDocumentsToolName = "search_documents";

    private const int MaximumQueryLength = 500;

    private static readonly IReadOnlyList<OllamaToolDefinition> Definitions =
        Array.AsReadOnly(new OllamaToolDefinition[]
        {
            CreateToolDefinition(
                SearchDocumentsToolName,
                "Aygaz sentetik e-ticaret demo dokümanlarında politika ve prosedür bilgisi arar. " +
                "İade, teslimat, kampanya veya müşteri destek sorularında kullan.",
                "query",
                "string",
                "Aranacak politika veya prosedür konusu.")
        });

    private readonly IDocumentRetrievalService _documentRetrievalService;
    private readonly IToolCallLogger _logger;

    public DocumentToolExecutor(
        IDocumentRetrievalService documentRetrievalService,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(documentRetrievalService);
        ArgumentNullException.ThrowIfNull(logger);

        _documentRetrievalService = documentRetrievalService;
        _logger = logger;
    }

    public IReadOnlyList<OllamaToolDefinition> ToolDefinitions => Definitions;

    public async Task<ToolExecutionResult> ExecuteAsync(
        string? toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogToolCall(toolName);

        ToolExecutionResult result = toolName switch
        {
            SearchDocumentsToolName =>
                await ExecuteSearchDocumentsAsync(arguments, cancellationToken),
            _ => ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.")
        };

        _logger.LogResult(result.Status);
        return result;
    }

    private async Task<ToolExecutionResult> ExecuteSearchDocumentsAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "query", out JsonElement queryElement)
            || queryElement.ValueKind != JsonValueKind.String)
        {
            _logger.LogArguments("query", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir doküman sorgusu belirtilmedi.");
        }

        string query = queryElement.GetString()?.Trim() ?? string.Empty;
        _logger.LogArguments("query", query);

        if (query.Length == 0 || query.Length > MaximumQueryLength)
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir doküman sorgusu belirtilmedi.");
        }

        IReadOnlyList<DocumentSearchResult> results;

        try
        {
            results = await _documentRetrievalService.SearchAsync(query, cancellationToken);
        }
        catch (ArgumentException)
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir doküman sorgusu belirtilmedi.");
        }

        if (results.Count == 0)
        {
            return ToolExecutionResult.DocumentsNotFound();
        }

        return ToolExecutionResult.FromSuccess(
            results.Select(result => new
            {
                documentName = result.DocumentName,
                text = result.Text
            }));
    }

    private static bool TryGetOnlyArgument(
        JsonElement arguments,
        string expectedName,
        out JsonElement value)
    {
        value = default;

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int propertyCount = 0;

        foreach (JsonProperty property in arguments.EnumerateObject())
        {
            propertyCount++;

            if (propertyCount > 1
                || !property.Name.Equals(expectedName, StringComparison.Ordinal))
            {
                return false;
            }

            value = property.Value;
        }

        return propertyCount == 1;
    }

    private static OllamaToolDefinition CreateToolDefinition(
        string name,
        string description,
        string argumentName,
        string argumentType,
        string argumentDescription)
    {
        var properties = new ReadOnlyDictionary<string, OllamaToolProperty>(
            new Dictionary<string, OllamaToolProperty>(StringComparer.Ordinal)
            {
                [argumentName] = new OllamaToolProperty(
                    argumentType,
                    argumentDescription)
            });

        return new OllamaToolDefinition(
            "function",
            new OllamaToolFunctionDefinition(
                name,
                description,
                new OllamaToolParameters(
                    "object",
                    properties,
                    Array.AsReadOnly(new[] { argumentName }),
                    AdditionalProperties: false)));
    }
}
