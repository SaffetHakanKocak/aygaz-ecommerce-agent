using Aygaz.ECommerce.Agent.Rag;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        float[] vector = [1f, 2f, 3f];

        double similarity = VectorMath.CosineSimilarity(vector, vector);

        Assert.Equal(1d, similarity, precision: 6);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        float[] left = [1f, 0f, 0f];
        float[] right = [0f, 1f, 0f];

        double similarity = VectorMath.CosineSimilarity(left, right);

        Assert.Equal(0d, similarity, precision: 6);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_ReturnsZero()
    {
        float[] left = [1f, 0f];
        float[] right = [1f, 0f, 0f];

        double similarity = VectorMath.CosineSimilarity(left, right);

        Assert.Equal(0d, similarity);
    }
}
