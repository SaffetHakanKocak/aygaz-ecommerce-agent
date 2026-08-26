using System.ComponentModel;
using Aygaz.ECommerce.Agent.Rag;
using Microsoft.SemanticKernel;

namespace Aygaz.ECommerce.SemanticKernel.Plugins;

public sealed record PolicySearchResult(string DocumentName, string Text, double Score);

public sealed class SupportPolicyPlugin
{
    private readonly IDocumentRetrievalService _documentRetrievalService;

    public SupportPolicyPlugin(IDocumentRetrievalService documentRetrievalService)
    {
        ArgumentNullException.ThrowIfNull(documentRetrievalService);
        _documentRetrievalService = documentRetrievalService;
    }

    [KernelFunction("search_support_policy")]
    [Description("Searches Aygaz support, return, delivery, and campaign policy documents.")]
    public async Task<IReadOnlyList<PolicySearchResult>> SearchSupportPolicyAsync(
        [Description("Policy question or search query.")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        IReadOnlyList<DocumentSearchResult> results = await _documentRetrievalService.SearchAsync(
            query.Trim(),
            cancellationToken);

        return results
            .Select(result => new PolicySearchResult(result.DocumentName, result.Text, result.Score))
            .ToArray();
    }
}
