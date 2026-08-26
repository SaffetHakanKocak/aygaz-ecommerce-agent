using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Aygaz.ECommerce.SemanticKernel.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class ExplicitCapabilityResolverTests
{
    [Fact]
    public void CustomerScopedOrderList_ResolvesOrder()
    {
        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "1 numaralı müşterinin siparişlerini göster.",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Order, capability);
    }

    [Fact]
    public void CustomerScopedAllOrders_ResolvesOrder_NotBulk()
    {
        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "1 numaralı müşterinin tüm siparişleri neler?",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Order, capability);
        Assert.False(ExplicitCapabilityResolver.IsCustomerScopedOrderListRequest(
            "tüm siparişleri getir"));
    }

    [Fact]
    public void ExplicitOrderNumber_OverridesCustomerHistoryContext()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "AYG-DEMO-1004 siparişinin bilgilerini getir.",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Order, capability);
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(
            history,
            "AYG-DEMO-1004 siparişinin bilgilerini getir."));
    }

    [Fact]
    public void ExplicitCustomerPhone_OverridesOrderHistoryContext()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "Mehmet Kaya'nın telefon numarası nedir?",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Customer, capability);
        Assert.False(OrderFollowUpResolver.IsOrderFollowUp(
            history,
            "Mehmet Kaya'nın telefon numarası nedir?"));
    }

    [Fact]
    public void OrderFollowUp_StillUsesHistory_WhenMessageIsReferential()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.False(ExplicitCapabilityResolver.TryResolve("durumu neydi?", out _));
        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(history, "durumu neydi?"));
    }

    [Fact]
    public void CustomerFollowUp_StillUsesHistory_WhenMessageIsReferential()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Mehmet Kaya (ID: 2, Ankara).");

        Assert.False(ExplicitCapabilityResolver.TryResolve("telefonu neydi?", out _));
        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(history, "telefonu neydi?"));
    }
}

public sealed class OrderListRoutingTests
{
    [Fact]
    public void CustomerScopedOrderList_IsNotBulkRequest()
    {
        Assert.False(OrderBulkRequestDetector.IsGlobalBulk("1 numaralı müşterinin siparişlerini göster."));
        Assert.False(OrderBulkRequestDetector.IsGlobalBulk("1 numaralı müşterinin tüm siparişleri neler?"));
    }

    [Fact]
    public void GlobalBulkOrderList_RemainsBlocked()
    {
        Assert.True(OrderBulkRequestDetector.IsGlobalBulk("tüm siparişleri getir"));
        Assert.True(OrderBulkRequestDetector.IsGlobalBulk("tüm sipariş bilgilerini göster"));
    }
}
