using System.Globalization;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Services;

internal enum ConversationEntityContext
{
    None,
    Customer,
    Order
}

internal static class ConversationContextResolver
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static ConversationEntityContext GetMostRecentEntityContext(ChatHistory? history)
    {
        if (history is null || history.Count == 0)
        {
            return ConversationEntityContext.None;
        }

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var message = history[i];
            if (message.Role != AuthorRole.Assistant || string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            string content = message.Content.ToLower(Turkish);
            if (IsOrderContext(content))
            {
                return ConversationEntityContext.Order;
            }

            if (IsCustomerContext(content))
            {
                return ConversationEntityContext.Customer;
            }
        }

        return ConversationEntityContext.None;
    }

    private static bool IsOrderContext(string content)
    {
        return content.Contains("sipariş", StringComparison.Ordinal)
               || content.Contains("siparis", StringComparison.Ordinal)
               || content.Contains("ayg-demo-", StringComparison.Ordinal);
    }

    private static bool IsCustomerContext(string content)
    {
        return content.Contains("müşteri", StringComparison.Ordinal)
               || content.Contains("musteri", StringComparison.Ordinal)
               || content.Contains("müşteri no", StringComparison.Ordinal)
               || content.Contains("ad soyad", StringComparison.Ordinal);
    }
}
