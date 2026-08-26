#pragma warning disable SKEXP0110

using Aygaz.AgentFramework.Agents;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.SemanticKernel.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using AgentDefinition = Aygaz.AgentFramework.Agents.AgentDefinition;

namespace Aygaz.ECommerce.SemanticKernel.Agents;

public static class OrderAgentRegistration
{
    public const string RouteKey = "order";
    public const string AgentName = "OrderAgent";

    public const string Instructions =
        "Only handle Aygaz order lookup and controlled order operation requests. Reply in Turkish using plain text only; never use markdown. " +
        "Use the available order functions when identifiers are available. " +
        "When the user provides a customer id and asks for that customer's orders " +
        "(e.g. '1 numaralı müşterinin siparişlerini göster' or '1 numaralı müşterinin tüm siparişlerini göster'), always call get_customer_orders with that customer id. " +
        "When conversation history already identifies a customer id and the user refers to that customer " +
        "(e.g. 'bu müşterinin siparişlerini göster', 'bu müşterinin tüm siparişlerini göster', 'onun siparişleri'), call get_customer_orders with that same customer id. " +
        "Customer-scoped order lists are supported; only system-wide all-order listing is forbidden. " +
        "When conversation history already identifies an order and the user asks a follow-up " +
        "(e.g. 'durumu neydi', 'sipariş numarası neydi', 'bilgilerini getir'), resolve that same order. " +
        "Controlled write operations are allowed only for explicit cancel or status update requests with an exact order number and a reason. " +
        "If the user asks to cancel or update an order without a reason, ask for the reason before calling a write function. " +
        "Never invent order data. Never create orders. Never list all system orders. " +
        "Do not expose internal function names. " +
        "If the user asks for an order by customer name without a customer id or order number, " +
        "ask for a customer number or order number instead of guessing. " +
        "Use recent conversation history only for follow-up order questions without explicit identifiers.";

    public static ChatCompletionAgent Register(
        IAgentRegistrar registrar,
        IOrderService orderService,
        IOrderOperationService orderOperationService,
        int maxOrderSearchResults = 5)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(orderService);
        ArgumentNullException.ThrowIfNull(orderOperationService);

        ChatCompletionAgent agent = registrar.Register(new AgentRegistration
        {
            RouteKey = RouteKey,
            Definition = new AgentDefinition
            {
                Name = AgentName,
                Instructions = Instructions
            },
            Plugins = [new OrderPlugin(orderService, orderOperationService, maxOrderSearchResults)]
        });

        agent.Kernel.AutoFunctionInvocationFilters.Add(new OrderExactLookupTerminationFilter());
        return agent;
    }
}
