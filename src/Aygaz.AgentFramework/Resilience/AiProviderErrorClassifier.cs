using System.Net;
using Aygaz.AgentFramework.Configuration;
using Microsoft.SemanticKernel;

namespace Aygaz.AgentFramework.Resilience;

public static class AiProviderErrorClassifier
{
    public static AiProviderErrorDetails Classify(
        Exception exception,
        SemanticKernelProvider provider)
    {
        Exception root = Unwrap(exception);
        int? statusCode = TryGetStatusCode(root);
        AiProviderErrorType errorType = ClassifyType(root, statusCode);
        bool isTransient = IsTransientStatus(statusCode)
                           || (errorType == AiProviderErrorType.HttpRequest && statusCode is null)
                           || errorType == AiProviderErrorType.Timeout;

        string message = SanitizeMessage(root.Message);
        return new AiProviderErrorDetails(provider, statusCode, errorType, message, isTransient);
    }

    public static bool ShouldRetry(AiProviderErrorDetails details, SemanticKernelProvider provider)
    {
        return provider == SemanticKernelProvider.Groq
               && details.IsTransient
               && details.ErrorType != AiProviderErrorType.Authentication;
    }

    private static Exception Unwrap(Exception exception)
    {
        Exception current = exception;
        while (current is AggregateException aggregate && aggregate.InnerException is not null)
        {
            current = aggregate.InnerException;
        }

        while (current.InnerException is not null
               && current is not HttpOperationException
               && current is not HttpRequestException)
        {
            current = current.InnerException;
        }

        return current;
    }

    private static int? TryGetStatusCode(Exception exception)
    {
        if (exception is HttpOperationException httpOperation)
        {
            return (int?)httpOperation.StatusCode;
        }

        foreach (object candidate in EnumerateSelfAndInners(exception))
        {
            System.Reflection.PropertyInfo? statusProperty = candidate.GetType().GetProperty("StatusCode");
            if (statusProperty?.GetValue(candidate) is HttpStatusCode status)
            {
                return (int)status;
            }

            if (statusProperty?.GetValue(candidate) is int numericStatus)
            {
                return numericStatus;
            }
        }

        return null;
    }

    private static IEnumerable<object> EnumerateSelfAndInners(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            yield return current;
            current = current.InnerException;
        }
    }

    private static AiProviderErrorType ClassifyType(Exception exception, int? statusCode)
    {
        if (statusCode == 429)
        {
            return AiProviderErrorType.RateLimit;
        }

        if (statusCode is 401 or 403)
        {
            return AiProviderErrorType.Authentication;
        }

        if (statusCode is 408 or 502 or 503 or 504)
        {
            return AiProviderErrorType.ServerError;
        }

        if (statusCode is >= 500)
        {
            return AiProviderErrorType.ServerError;
        }

        if (exception is HttpOperationException)
        {
            return AiProviderErrorType.ProviderError;
        }

        if (exception is HttpRequestException)
        {
            return AiProviderErrorType.HttpRequest;
        }

        if (IsTimeout(exception))
        {
            return AiProviderErrorType.Timeout;
        }

        return AiProviderErrorType.Unexpected;
    }

    private static bool IsTransientStatus(int? statusCode)
    {
        return statusCode is 429 or 408 or 502 or 503 or 504;
    }

    private static bool IsTimeout(Exception exception)
    {
        return exception is TimeoutException
               || (exception is TaskCanceledException && exception.InnerException is TimeoutException)
               || exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown provider error.";
        }

        string sanitized = message;
        sanitized = RedactSecrets(sanitized, "gsk_");
        sanitized = RedactSecrets(sanitized, "sk-");
        sanitized = RedactSecrets(sanitized, "Bearer ");
        return sanitized.Length > 500 ? sanitized[..500] : sanitized;
    }

    private static string RedactSecrets(string input, string prefix)
    {
        int index = input.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return input;
        }

        int end = index + prefix.Length;
        while (end < input.Length && !char.IsWhiteSpace(input[end]))
        {
            end++;
        }

        return input[..index] + prefix + "[REDACTED]" + input[end..];
    }
}
