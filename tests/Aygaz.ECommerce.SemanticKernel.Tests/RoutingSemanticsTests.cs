using Aygaz.ECommerce.SemanticKernel.Capabilities;
using Aygaz.ECommerce.SemanticKernel.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class AygazCompanyInfoResolverTests
{
    [Theory]
    [InlineData("Aygaz CEO kim")]
    [InlineData("Aygaz'ın CEO'su kim")]
    [InlineData("Aygaz ne zaman kuruldu")]
    [InlineData("Aygaz genel merkezi nerede")]
    [InlineData("Aygaz hakkında bilgi verir misin")]
    [InlineData("Aygaz çalışan sayısı kaç")]
    public void CompanyInfo_IsAllowedUnknown(string message)
    {
        Assert.True(AygazCompanyInfoResolver.TryResolve(message, out AygazCapability capability));
        Assert.Equal(AygazCapability.Unknown, capability);
    }

    [Fact]
    public void Revenue_IsAllowedSales()
    {
        Assert.True(AygazCompanyInfoResolver.TryResolve("Aygaz cirosu ne kadar?", out AygazCapability capability));
        Assert.Equal(AygazCapability.Sales, capability);
    }

    [Fact]
    public void CustomerLookup_IsNotCompanyInfo()
    {
        Assert.False(AygazCompanyInfoResolver.TryResolve("Ahmet Yılmaz'ın bilgilerini getir", out _));
    }

    [Fact]
    public void ExplicitCapability_DoesNotRouteCompanyInfoToCustomer()
    {
        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "Aygaz hakkında bilgi verir misin",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Unknown, capability);
    }
}

public sealed class OrderBulkRequestDetectorTests
{
    [Theory]
    [InlineData("tüm siparişleri göster")]
    [InlineData("tüm siparişleri getir")]
    [InlineData("tüm sipariş bilgilerini göster")]
    [InlineData("bütün siparişleri getir")]
    [InlineData("bütün siparişlerin bilgilerini göster")]
    public void GlobalBulk_IsDetected(string message)
    {
        Assert.True(OrderBulkRequestDetector.IsGlobalBulk(message));
    }

    [Fact]
    public void GlobalBulk_RemainsBlocked_EvenWithCustomerHistory()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(OrderBulkRequestDetector.IsGlobalBulk("tüm sipariş bilgilerini göster"));
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(history, "tüm sipariş bilgilerini göster"));
        Assert.False(ReferentialMessageDetector.IsReferentialFollowUp("tüm sipariş bilgilerini göster"));
    }

    [Theory]
    [InlineData("1 numaralı müşterinin siparişlerini göster")]
    [InlineData("1 numaralı müşterinin tüm siparişlerini göster")]
    [InlineData("bu müşterinin tüm siparişlerini göster")]
    public void CustomerScoped_IsNotGlobalBulk(string message)
    {
        Assert.False(OrderBulkRequestDetector.IsGlobalBulk(message));
        Assert.True(ExplicitCapabilityResolver.IsCustomerScopedOrderListRequest(message)
                    || OrderBulkRequestDetector.IsReferentialCustomerScope(message));
    }

    [Fact]
    public void ReferentialCustomerOrders_ResolvesOrder()
    {
        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "bu müşterinin tüm siparişlerini göster",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Order, capability);
    }

    [Fact]
    public void CustomerBulk_IsDetected()
    {
        string message = "tüm müşteri bilgilerini göster";
        Assert.True(
            message.Contains("tüm müşteri bilgilerini", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tum musteri bilgilerini", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class RoutingSemanticsHistoryTests
{
    [Fact]
    public void CustomerHistory_PlusExplicitOrder_IsOrder()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "AYG-DEMO-1004 siparişinin bilgilerini getir",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Order, capability);
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(
            history,
            "AYG-DEMO-1004 siparişinin bilgilerini getir"));
    }

    [Fact]
    public void OrderHistory_PlusExplicitCustomer_IsCustomer()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "Mehmet Kaya'nın telefonunu getir",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Customer, capability);
        Assert.False(OrderFollowUpResolver.IsOrderFollowUp(
            history,
            "Mehmet Kaya'nın telefonunu getir"));
    }

    [Fact]
    public void CustomerHistory_PlusCompanyInfo_IsUnknown_NotCustomer()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(AygazCompanyInfoResolver.TryResolve(
            "Aygaz'ın CEO'su kim?",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Unknown, capability);
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(history, "Aygaz'ın CEO'su kim?"));
        Assert.False(ReferentialMessageDetector.IsReferentialFollowUp("Aygaz'ın CEO'su kim?"));
    }

    [Fact]
    public void OrderContext_DurumuNeydi_IsOrderFollowUp()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(history, "durumu neydi?"));
        Assert.True(ReferentialMessageDetector.IsReferentialFollowUp("durumu neydi?"));
    }

    [Fact]
    public void CustomerContext_TelefonuNeydi_IsCustomerFollowUp()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Mehmet Kaya (ID: 2, Ankara).");

        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(history, "telefonu neydi?"));
    }

    [Fact]
    public void CustomerContext_TumBilgileriniGetir_IsCustomerFollowUp()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(history, "tüm bilgilerini getir"));
        Assert.Equal(
            ConversationEntityContext.Customer,
            ConversationContextResolver.GetMostRecentEntityContext(history));
    }

    [Fact]
    public void OrderContext_TumBilgileriniGetir_IsOrderFollowUp()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Sipariş AYG-DEMO-1004 durumu: Hazırlanıyor.");

        Assert.True(OrderFollowUpResolver.IsOrderFollowUp(history, "tüm bilgilerini getir"));
        Assert.Equal(
            ConversationEntityContext.Order,
            ConversationContextResolver.GetMostRecentEntityContext(history));
    }

    [Fact]
    public void ClearSession_RemovesPriorContext()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");
        history.Clear();

        Assert.Equal(
            ConversationEntityContext.None,
            ConversationContextResolver.GetMostRecentEntityContext(history));
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(history, "telefonu neydi?"));
    }

    [Fact]
    public void CustomerId2Context_ReferentialAllOrders_IsOrderNotBulk()
    {
        var history = new ChatHistory();
        history.AddAssistantMessage("Müşteri: Mehmet Kaya (ID: 2, Ankara).");

        Assert.False(OrderBulkRequestDetector.IsGlobalBulk("bu müşterinin tüm siparişlerini göster"));
        Assert.True(ExplicitCapabilityResolver.TryResolve(
            "bu müşterinin tüm siparişlerini göster",
            out AygazCapability capability));
        Assert.Equal(AygazCapability.Order, capability);
        Assert.True(CustomerFollowUpResolver.HistoryHasCustomerReference(history));
    }

    [Theory]
    [InlineData("BJK maçı ne olur")]
    [InlineData("Trendyol siparişimi göster")]
    public void OutOfScopeMarkers_AreNotCompanyInfo(string message)
    {
        Assert.False(AygazCompanyInfoResolver.TryResolve(message, out _));
    }
}
