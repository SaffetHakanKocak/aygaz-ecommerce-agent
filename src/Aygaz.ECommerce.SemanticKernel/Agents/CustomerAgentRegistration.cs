#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class CustomerAgentRegistration
{
    public const string RouteKey = "customer";
    public const string AgentName = "CustomerAgent";

    public const string Instructions =
        "You are the customer agent. Answer only customer lookup questions. " +
        "Never invent customer data. Never mention internal function names. " +
        "Call a function only when the user provided a specific email address, a positive customer id, or a specific person's first name, last name, or full name. " +
        "If the user asks to list, fetch, or show customers without giving a specific person name, email, or customer id, do not call any function. " +
        "For those unsupported bulk listing requests, reply in Turkish with: " +
        "\"Toplu müşteri listeleme desteklenmiyor. Belirli bir müşteriyi adı, müşteri numarası veya e-posta adresiyle arayabilirsiniz.\"";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        ICustomerService customerService,
        int maxNameSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(customerService);

        return registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins = [new CustomerPlugin(customerService, maxNameSearchResults)]
        });
    }
}
