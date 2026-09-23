using Aygaz.ECommerce.Agent.Rag;
using Aygaz.ECommerce.Agent.Services;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class LexicalEmbeddingServiceTests
{
    [Fact]
    public async Task EmbedQueryAsync_ReturnPolicyQuery_RanksRelatedTextHigher()
    {
        var service = new LexicalEmbeddingService();

        IReadOnlyList<float> query = await service.EmbedQueryAsync("iade suresi kac gun");
        IReadOnlyList<float> related = await service.EmbedDocumentAsync(
            "Aygaz demo iade suresi teslimattan sonra 14 gun olarak uygulanir.");
        IReadOnlyList<float> unrelated = await service.EmbedDocumentAsync(
            "Stok lokasyonlari ve depo transfer planlari operasyon ekibi tarafindan izlenir.");

        double relatedScore = VectorMath.CosineSimilarity(query, related);
        double unrelatedScore = VectorMath.CosineSimilarity(query, unrelated);

        Assert.True(relatedScore > unrelatedScore);
    }
}
