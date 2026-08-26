using Aygaz.ECommerce.Agent.Rag;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal sealed class ScopedDocumentRetrievalServiceAccessor : IDocumentRetrievalService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedDocumentRetrievalServiceAccessor(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public int DocumentCount => Execute(service => service.DocumentCount);

    public int ChunkCount => Execute(service => service.ChunkCount);

    public Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.SearchAsync(query, cancellationToken));
    }

    private T Execute<T>(Func<IDocumentRetrievalService, T> action)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDocumentRetrievalService>();
        return action(service);
    }

    private async Task<T> ExecuteAsync<T>(Func<IDocumentRetrievalService, Task<T>> action)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IDocumentRetrievalService>();
        return await action(service);
    }
}
