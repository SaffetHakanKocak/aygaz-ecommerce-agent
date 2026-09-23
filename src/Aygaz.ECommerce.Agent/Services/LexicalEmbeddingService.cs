using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class LexicalEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 384;
    private static readonly Regex TokenPattern = new(@"\p{L}+|\d+", RegexOptions.Compiled);

    public Task<IReadOnlyList<float>> EmbedDocumentAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return EmbedAsync(input, cancellationToken);
    }

    public Task<IReadOnlyList<float>> EmbedQueryAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return EmbedAsync(input, cancellationToken);
    }

    private static Task<IReadOnlyList<float>> EmbedAsync(string input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        float[] vector = new float[Dimensions];
        foreach (Match match in TokenPattern.Matches(Normalize(input)))
        {
            string token = match.Value;
            int index = StableIndex(token);
            vector[index] += 1f;
        }

        return Task.FromResult<IReadOnlyList<float>>(vector);
    }

    private static int StableIndex(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToUInt16(hash, 0) % Dimensions;
    }

    private static string Normalize(string input)
    {
        string lower = input.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        string decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char character in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ö', 'o')
            .Replace('ç', 'c')
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
}
