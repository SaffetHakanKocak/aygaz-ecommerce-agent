namespace Aygaz.ECommerce.Agent.Rag;

public static class VectorMath
{
    public static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Count == 0 || right.Count != left.Count)
        {
            return 0d;
        }

        double dotProduct = 0d;
        double magnitudeLeft = 0d;
        double magnitudeRight = 0d;

        for (int index = 0; index < left.Count; index++)
        {
            float leftValue = left[index];
            float rightValue = right[index];
            dotProduct += leftValue * rightValue;
            magnitudeLeft += leftValue * leftValue;
            magnitudeRight += rightValue * rightValue;
        }

        if (magnitudeLeft == 0d || magnitudeRight == 0d)
        {
            return 0d;
        }

        return dotProduct / (Math.Sqrt(magnitudeLeft) * Math.Sqrt(magnitudeRight));
    }
}
