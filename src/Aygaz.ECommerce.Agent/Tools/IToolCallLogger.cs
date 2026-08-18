namespace Aygaz.ECommerce.Agent.Tools;

public interface IToolCallLogger
{
    void LogToolCall(string? toolName);

    void LogArguments(string argumentName, string? argumentValue);

    void LogResult(ToolExecutionStatus status);
}
