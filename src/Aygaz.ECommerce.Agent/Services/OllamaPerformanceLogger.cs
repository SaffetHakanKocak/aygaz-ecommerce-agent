using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aygaz.ECommerce.Agent.Services;

public sealed class OllamaPerformanceLogger : IOllamaPerformanceLogger
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<OllamaPerformanceLogger> _logger;

    public OllamaPerformanceLogger(
        IHostEnvironment environment,
        ILogger<OllamaPerformanceLogger> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public void LogCall(OllamaCallMetrics metrics)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        _logger.LogInformation(
            "Ollama çağrısı: model={Model}, callType={CallType}, totalDuration={TotalDuration}, " +
            "loadDuration={LoadDuration}, promptEvalDuration={PromptEvalDuration}, " +
            "evalDuration={EvalDuration}, promptEvalCount={PromptEvalCount}, evalCount={EvalCount}",
            metrics.Model,
            metrics.CallType,
            OllamaDurationFormatter.FormatNanoseconds(metrics.TotalDurationNs),
            OllamaDurationFormatter.FormatNanoseconds(metrics.LoadDurationNs),
            OllamaDurationFormatter.FormatNanoseconds(metrics.PromptEvalDurationNs),
            OllamaDurationFormatter.FormatNanoseconds(metrics.EvalDurationNs),
            metrics.PromptEvalCount ?? 0,
            metrics.EvalCount ?? 0);
    }

    public void LogRequestSummary(
        int callCount,
        long elapsedMilliseconds,
        bool fastPathUsed)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        _logger.LogInformation(
            "Ollama request özeti: ollamaCallCount={OllamaCallCount}, totalElapsedMs={TotalElapsedMs}, " +
            "fastPathUsed={FastPathUsed}",
            callCount,
            elapsedMilliseconds,
            fastPathUsed);
    }
}
