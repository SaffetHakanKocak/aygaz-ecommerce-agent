using Aygaz.ECommerce.SemanticKernel.Formatting;
using Aygaz.ECommerce.SemanticKernel.Services;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Aygaz.ECommerce.SemanticKernel.Tests;

public sealed class CustomerFollowUpResolverTests
{
    [Theory]
    [InlineData("tüm bilgilerini getir")]
    [InlineData("bilgilerini getir")]
    [InlineData("telefonu neydi?")]
    [InlineData("adresi neydi")]
    [InlineData("e-postası neydi?")]
    [InlineData("hangi şehirdeydi?")]
    [InlineData("nerede yaşıyordu")]
    [InlineData("müşteri numarası neydi")]
    public void FollowUpPhrase_WithCustomerHistory_Resolves(string message)
    {
        var history = new ChatHistory();
        history.AddUserMessage("Ahmet Yılmaz'ın bilgileri");
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(history, message));
    }

    [Fact]
    public void FullInfoFollowUp_ResolvesPreviousCustomer_WithoutHistory_DoesNot()
    {
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(null, "tüm bilgilerini getir"));
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(new ChatHistory(), "tüm bilgilerini getir"));
    }

    [Fact]
    public void IdBasedPreviousCustomer_Resolves()
    {
        var history = new ChatHistory();
        history.AddUserMessage("ID'si 2 olan müşteri kim?");
        history.AddAssistantMessage("Müşteri: Mehmet Kaya (ID: 2, Ankara).");

        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(history, "tüm bilgilerini getir"));
    }

    [Fact]
    public void CityFollowUp_Resolves()
    {
        var history = new ChatHistory();
        history.AddUserMessage("Ahmet Yılmaz'ın bilgileri");
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(history, "hangi şehirdeydi?"));
    }

    [Fact]
    public void ClearRemovesReference()
    {
        var history = new ChatHistory();
        history.AddUserMessage("Ahmet Yılmaz'ın bilgileri");
        history.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");
        Assert.True(CustomerFollowUpResolver.HistoryHasCustomerReference(history));

        var cleared = new ChatHistory();
        Assert.False(CustomerFollowUpResolver.IsCustomerFollowUp(cleared, "tüm bilgilerini getir"));
    }

    [Fact]
    public void SeparateSessions_Isolated()
    {
        var sessionA = new ChatHistory();
        sessionA.AddAssistantMessage("Müşteri: Ahmet Yılmaz (ID: 1, İstanbul).");

        var sessionB = new ChatHistory();
        sessionB.AddAssistantMessage("Müşteri: Mehmet Kaya (ID: 2, Ankara).");

        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(sessionA, "tüm bilgilerini getir"));
        Assert.True(CustomerFollowUpResolver.IsCustomerFollowUp(sessionB, "tüm bilgilerini getir"));
        Assert.DoesNotContain("Mehmet", sessionA[^1].Content!, StringComparison.Ordinal);
        Assert.DoesNotContain("Ahmet", sessionB[^1].Content!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tüm bilgilerini getir")]
    [InlineData("bilgilerini getir")]
    [InlineData("tüm bilgileri neydi")]
    public void FullInfoFollowUp_FormatsFullProfile(string userMessage)
    {
        var customer = new Aygaz.ECommerce.Agent.Models.Agent.CustomerAgentResult(
            1,
            "Ahmet",
            "Yılmaz",
            "ahmet.yilmaz@example.com",
            "İstanbul",
            "0532 000 00 01",
            "Atatürk Mah. Örnek Sok. No: 10");

        Assert.Equal(CustomerLookupDetail.Full, CustomerLookupResponseFormatter.ResolveDetail(userMessage));

        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "search_customers_by_name",
            new[] { customer },
            userMessage,
            out string text);

        Assert.True(formatted);
        Assert.Contains("Müşteri Bilgileri", text, StringComparison.Ordinal);
        Assert.Contains("Ahmet Yılmaz", text, StringComparison.Ordinal);
        Assert.Contains("0532 000 00 01", text, StringComparison.Ordinal);
        Assert.Contains("ahmet.yilmaz@example.com", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CityFollowUp_FormatsCityOnly()
    {
        var customer = new Aygaz.ECommerce.Agent.Models.Agent.CustomerAgentResult(
            1,
            "Ahmet",
            "Yılmaz",
            "ahmet.yilmaz@example.com",
            "İstanbul",
            "0532 000 00 01",
            "Atatürk Mah. Örnek Sok. No: 10");

        Assert.Equal(CustomerLookupDetail.City, CustomerLookupResponseFormatter.ResolveDetail("hangi şehirdeydi?"));

        bool formatted = CustomerLookupResponseFormatter.TryFormatExactLookup(
            "search_customers_by_name",
            new[] { customer },
            "hangi şehirdeydi?",
            out string text);

        Assert.True(formatted);
        Assert.Contains("İstanbul", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0532", text, StringComparison.Ordinal);
    }
}
