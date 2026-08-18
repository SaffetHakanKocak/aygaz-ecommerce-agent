using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.Agent.Configuration;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerAgent(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AgentOptions>()
            .Bind(configuration.GetRequiredSection(AgentOptions.SectionName))
            .Validate(
                options => options.MaxToolIterations is >= 1 and <= 10,
                "Agent:MaxToolIterations 1 ile 10 arasında olmalıdır.")
            .Validate(
                options => options.MaxToolCallsPerIteration is >= 1 and <= 10,
                "Agent:MaxToolCallsPerIteration 1 ile 10 arasında olmalıdır.")
            .Validate(
                options => options.MaxNameSearchResults is >= 1 and <= 20,
                "Agent:MaxNameSearchResults 1 ile 20 arasında olmalıdır.")
            .Validate(
                options => options.MaxConversationTurns is >= 1 and <= 10,
                "Agent:MaxConversationTurns 1 ile 10 arasında olmalıdır.");

        services.AddScoped<IToolCallLogger, ConsoleToolCallLogger>();
        services.AddScoped<IAgentToolExecutor, CustomerToolExecutor>();
        services.AddScoped<IAgentService, OllamaAgentService>();
        services.AddScoped<ConsoleCustomerAgentRunner>();

        return services;
    }
}
