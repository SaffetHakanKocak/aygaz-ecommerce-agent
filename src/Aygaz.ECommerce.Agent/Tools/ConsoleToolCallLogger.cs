using System.Text;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class ConsoleToolCallLogger : IToolCallLogger
{
    private const int MaximumLoggedValueLength = 120;

    public void LogToolCall(string? toolName)
    {
        Console.WriteLine($"[Tool] {Sanitize(toolName)}");
    }

    public void LogArguments(string argumentName, string? argumentValue)
    {
        string safeName = Sanitize(argumentName);
        string safeValue = safeName switch
        {
            _ when safeName.Equals("email", StringComparison.OrdinalIgnoreCase) =>
                SanitizeEmail(argumentValue),
            _ when safeName.Equals("orderNumber", StringComparison.OrdinalIgnoreCase) =>
                SanitizeOrderNumber(argumentValue),
            _ when safeName.Equals("query", StringComparison.OrdinalIgnoreCase) =>
                "<redacted>",
            _ => Sanitize(argumentValue)
        };

        Console.WriteLine($"[Args] {safeName}={safeValue}");
    }

    public void LogResult(ToolExecutionStatus status)
    {
        Console.WriteLine($"[Tool Result] {status}");
    }

    private static string SanitizeEmail(string? value)
    {
        string sanitized = Sanitize(value);
        int separatorIndex = sanitized.LastIndexOf('@');

        if (separatorIndex <= 0 || separatorIndex == sanitized.Length - 1)
        {
            return "<redacted>";
        }

        string domain = sanitized[(separatorIndex + 1)..];
        if (domain.Equals("example.com", StringComparison.OrdinalIgnoreCase))
        {
            return sanitized;
        }

        string localPart = sanitized[..separatorIndex];
        return $"{localPart[0]}***@{domain}";
    }

    private static string SanitizeOrderNumber(string? value)
    {
        const string DemoPrefix = "AYG-DEMO-";

        string sanitized = Sanitize(value);
        if (!sanitized.StartsWith(DemoPrefix, StringComparison.OrdinalIgnoreCase)
            || sanitized.Length == DemoPrefix.Length)
        {
            return "<redacted>";
        }

        foreach (char character in sanitized.AsSpan(DemoPrefix.Length))
        {
            if (character is < '0' or > '9')
            {
                return "<redacted>";
            }
        }

        return sanitized;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        var builder = new StringBuilder(Math.Min(value.Length, MaximumLoggedValueLength));

        foreach (char character in value)
        {
            if (builder.Length >= MaximumLoggedValueLength)
            {
                break;
            }

            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        string sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? "<empty>" : sanitized;
    }
}
