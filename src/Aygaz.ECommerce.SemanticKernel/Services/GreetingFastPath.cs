namespace Aygaz.ECommerce.SemanticKernel.Services;

internal static class GreetingFastPath
{
    private static readonly HashSet<string> Greetings = new(StringComparer.OrdinalIgnoreCase)
    {
        "selam",
        "merhaba",
        "günaydın",
        "iyi günler",
        "iyi akşamlar"
    };

    public const string GreetingResponse = "Merhaba! Aygaz ile ilgili nasıl yardımcı olabilirim?";

    public static bool IsGreeting(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return Greetings.Contains(message.Trim());
    }
}
