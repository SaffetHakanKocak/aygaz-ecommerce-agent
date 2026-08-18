using System.Text.Json;

namespace Aygaz.ECommerce.Agent.Tools;

public enum ToolExecutionStatus
{
    Success,
    NotFound,
    Rejected
}

public sealed record ToolExecutionResult(string Content, ToolExecutionStatus Status)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static ToolExecutionResult FromSuccess<T>(T data)
    {
        string content = JsonSerializer.Serialize(
            new
            {
                success = true,
                found = true,
                data
            },
            JsonOptions);

        return new ToolExecutionResult(content, ToolExecutionStatus.Success);
    }

    public static ToolExecutionResult CustomerNotFound()
    {
        string content = JsonSerializer.Serialize(
            new
            {
                success = true,
                found = false,
                message = "Müşteri bulunamadı."
            },
            JsonOptions);

        return new ToolExecutionResult(content, ToolExecutionStatus.NotFound);
    }

    public static ToolExecutionResult OrderNotFound()
    {
        string content = JsonSerializer.Serialize(
            new
            {
                success = true,
                found = false,
                message = "Sipariş bulunamadı."
            },
            JsonOptions);

        return new ToolExecutionResult(content, ToolExecutionStatus.NotFound);
    }

    public static ToolExecutionResult Rejected(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        string content = JsonSerializer.Serialize(
            new
            {
                success = false,
                error
            },
            JsonOptions);

        return new ToolExecutionResult(content, ToolExecutionStatus.Rejected);
    }
}
