using Aygaz.AgentFramework.Configuration;

namespace Aygaz.AgentFramework.Resilience;

public static class AiProviderRetryExecutor
{
    public const int MaxRetryCount = 1;

    private static readonly Random Jitter = new();

    public static async Task<T> ExecuteAsync<T>(
        SemanticKernelProvider provider,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                AiProviderErrorDetails details = AiProviderErrorClassifier.Classify(ex, provider);
                if (attempt < MaxRetryCount && AiProviderErrorClassifier.ShouldRetry(details, provider))
                {
                    attempt++;
                    await DelayBeforeRetryAsync(cancellationToken);
                    continue;
                }

                if (provider == SemanticKernelProvider.Groq && details.IsTransient)
                {
                    throw new AiProviderTemporarilyUnavailableException(details, ex);
                }

                throw;
            }
        }
    }

    public static async IAsyncEnumerable<T> ExecuteStreamingAsync<T>(
        SemanticKernelProvider provider,
        Func<CancellationToken, IAsyncEnumerable<T>> streamFactory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int attempt = 0;

        while (true)
        {
            bool yieldedAny = false;
            IAsyncEnumerable<T> stream;

            try
            {
                stream = streamFactory(cancellationToken);
            }
            catch (Exception ex) when (TryPrepareRetry(ex, provider, ref attempt, out AiProviderErrorDetails? _))
            {
                await DelayBeforeRetryAsync(cancellationToken);
                continue;
            }

            await using IAsyncEnumerator<T> enumerator = stream.GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception ex) when (!yieldedAny && TryPrepareRetry(ex, provider, ref attempt, out _))
                {
                    await DelayBeforeRetryAsync(cancellationToken);
                    goto RetryStream;
                }

                if (!hasNext)
                {
                    yield break;
                }

                yieldedAny = true;
                yield return enumerator.Current;
            }

            RetryStream: ;
        }
    }

    private static bool TryPrepareRetry(
        Exception exception,
        SemanticKernelProvider provider,
        ref int attempt,
        out AiProviderErrorDetails? details)
    {
        if (exception is OperationCanceledException)
        {
            details = null;
            return false;
        }

        details = AiProviderErrorClassifier.Classify(exception, provider);
        if (attempt >= MaxRetryCount || !AiProviderErrorClassifier.ShouldRetry(details, provider))
        {
            if (provider == SemanticKernelProvider.Groq && details.IsTransient)
            {
                throw new AiProviderTemporarilyUnavailableException(details, exception);
            }

            throw exception;
        }

        attempt++;
        return true;
    }

    internal static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken)
    {
        int delayMs = Jitter.Next(750, 1501);
        await Task.Delay(delayMs, cancellationToken);
    }
}
