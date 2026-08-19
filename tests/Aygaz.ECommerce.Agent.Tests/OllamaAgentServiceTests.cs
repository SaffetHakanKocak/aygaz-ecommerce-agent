using System.Text.Json;
using Aygaz.ECommerce.Agent.Agent;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Services;
using Aygaz.ECommerce.Agent.Tools;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tests;

public sealed class OllamaAgentServiceTests
{
    [Fact]
    public async Task AskAsync_ToolCall_AppendsAssistantAndToolMessagesBeforeFinalRequest()
    {
        JsonElement arguments = ParseJson(
            """{"email":"ahmet.yilmaz@example.com"}""");
        var toolCall = new OllamaToolCall(
            "call-1",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByEmailToolName,
                Arguments: arguments));
        var firstAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [toolCall]);
        var finalAssistantMessage = new OllamaChatMessage(
            "assistant",
            "Ahmet Yılmaz, İstanbul'da kayıtlı müşteridir.");
        var chatClient = new RecordingOllamaChatClient(
            firstAssistantMessage,
            finalAssistantMessage);
        ToolExecutionResult toolResult = ToolExecutionResult.FromSuccess(
            new
            {
                id = 1,
                firstName = "Ahmet",
                lastName = "Yılmaz",
                email = "ahmet.yilmaz@example.com",
                city = "İstanbul"
            });
        var toolExecutor = new RecordingAgentToolExecutor(toolResult);
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync(
            "  ahmet.yilmaz@example.com müşterisi kim?  ");

        Assert.Equal("Ahmet Yılmaz, İstanbul'da kayıtlı müşteridir.", answer);
        Assert.Equal(2, chatClient.Calls.Count);
        Assert.Single(toolExecutor.Calls);

        ChatInvocation firstRequest = chatClient.Calls[0];
        Assert.Equal(
            new[] { "system", "user" },
            firstRequest.Messages.Select(message => message.Role));
        Assert.False(string.IsNullOrWhiteSpace(firstRequest.Messages[0].Content));
        Assert.Equal(
            "ahmet.yilmaz@example.com müşterisi kim?",
            firstRequest.Messages[1].Content);

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal(firstAssistantMessage, secondRequest.Messages[2]);

        OllamaChatMessage toolMessage = secondRequest.Messages[3];
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, toolMessage.ToolName);
        Assert.Equal("call-1", toolMessage.ToolCallId);
        Assert.Equal(toolResult.Content, toolMessage.Content);
        Assert.Null(toolMessage.ToolCalls);

        ToolInvocation execution = Assert.Single(toolExecutor.Calls);
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, execution.ToolName);
        Assert.Equal(
            "ahmet.yilmaz@example.com",
            execution.Arguments.GetProperty("email").GetString());

        Assert.All(
            chatClient.Calls,
            call =>
            {
                OllamaChatSettings settings = Assert.IsType<OllamaChatSettings>(call.Settings);
                Assert.Same(toolExecutor.ToolDefinitions, settings.Tools);
            });
    }

    [Fact]
    public async Task AskAsync_EmailThenLatestOrder_UsesThreeModelResponsesAndExactToolHistory()
    {
        var customerToolCall = new OllamaToolCall(
            "call-customer",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByEmailToolName,
                Arguments: ParseJson(
                    """{"email":"ahmet.yilmaz@example.com"}""")));
        var orderToolCall = new OllamaToolCall(
            "call-order",
            new OllamaToolCallFunction(
                Name: OrderToolExecutor.GetLatestCustomerOrderToolName,
                Arguments: ParseJson("""{"customerId":1}""")));
        var customerAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [customerToolCall]);
        var orderAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [orderToolCall]);
        var finalAssistantMessage = new OllamaChatMessage(
            "assistant",
            "En son sipari\u015f AYG-DEMO-1004 ve durumu Haz\u0131rlan\u0131yor.");
        var chatClient = new RecordingOllamaChatClient(
            customerAssistantMessage,
            orderAssistantMessage,
            finalAssistantMessage);
        ToolExecutionResult customerResult = ToolExecutionResult.FromSuccess(
            new
            {
                id = 1,
                firstName = "Ahmet",
                lastName = "Y\u0131lmaz",
                email = "ahmet.yilmaz@example.com",
                city = "\u0130stanbul"
            });
        ToolExecutionResult orderResult = ToolExecutionResult.FromSuccess(
            new
            {
                id = 4,
                orderNumber = "AYG-DEMO-1004",
                orderDate = new DateTime(2026, 2, 22, 10, 0, 0, DateTimeKind.Utc),
                status = "Haz\u0131rlan\u0131yor",
                totalAmount = 910.75m
            });
        var toolExecutor = new RecordingAgentToolExecutor(
            customerResult,
            orderResult);
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync(
            "  ahmet.yilmaz@example.com m\u00fc\u015fterisinin en son sipari\u015fi nedir?  ");

        Assert.Equal(
            "En son sipari\u015f AYG-DEMO-1004 ve durumu Haz\u0131rlan\u0131yor.",
            answer);
        Assert.Equal(3, chatClient.Calls.Count);
        Assert.Equal(2, toolExecutor.Calls.Count);
        Assert.Equal(
            new[]
            {
                CustomerToolExecutor.GetCustomerByEmailToolName,
                OrderToolExecutor.GetLatestCustomerOrderToolName
            },
            toolExecutor.Calls.Select(call => call.ToolName));
        Assert.Equal(
            "ahmet.yilmaz@example.com",
            toolExecutor.Calls[0].Arguments.GetProperty("email").GetString());
        Assert.Equal(
            1,
            toolExecutor.Calls[1].Arguments.GetProperty("customerId").GetInt32());

        ChatInvocation firstRequest = chatClient.Calls[0];
        Assert.Equal(
            new[] { "system", "user" },
            firstRequest.Messages.Select(message => message.Role));
        Assert.Equal(
            "ahmet.yilmaz@example.com m\u00fc\u015fterisinin en son sipari\u015fi nedir?",
            firstRequest.Messages[1].Content);

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal(customerAssistantMessage, secondRequest.Messages[2]);
        Assert.Equal("tool", secondRequest.Messages[3].Role);
        Assert.Equal(CustomerToolExecutor.GetCustomerByEmailToolName, secondRequest.Messages[3].ToolName);
        Assert.Equal("call-customer", secondRequest.Messages[3].ToolCallId);
        Assert.Equal(customerResult.Content, secondRequest.Messages[3].Content);
        Assert.Null(secondRequest.Messages[3].ToolCalls);

        ChatInvocation thirdRequest = chatClient.Calls[2];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool", "assistant", "tool" },
            thirdRequest.Messages.Select(message => message.Role));
        Assert.Equal(customerAssistantMessage, thirdRequest.Messages[2]);
        Assert.Equal(secondRequest.Messages[3], thirdRequest.Messages[3]);
        Assert.Equal(orderAssistantMessage, thirdRequest.Messages[4]);
        Assert.Equal("tool", thirdRequest.Messages[5].Role);
        Assert.Equal(OrderToolExecutor.GetLatestCustomerOrderToolName, thirdRequest.Messages[5].ToolName);
        Assert.Equal("call-order", thirdRequest.Messages[5].ToolCallId);
        Assert.Equal(orderResult.Content, thirdRequest.Messages[5].Content);
        Assert.Null(thirdRequest.Messages[5].ToolCalls);

        Assert.All(
            chatClient.Calls,
            call =>
            {
                OllamaChatSettings settings = Assert.IsType<OllamaChatSettings>(call.Settings);
                Assert.Same(toolExecutor.ToolDefinitions, settings.Tools);
            });
    }

    [Fact]
    public async Task AskAsync_SkuThenInventory_UsesThreeModelResponsesAndExactToolHistory()
    {
        var productToolCall = new OllamaToolCall(
            "call-product",
            new OllamaToolCallFunction(
                Name: ProductToolExecutor.GetProductBySkuToolName,
                Arguments: ParseJson(
                    """{"sku":"AYG-DEMO-PRD-001"}""")));
        var inventoryToolCall = new OllamaToolCall(
            "call-inventory",
            new OllamaToolCallFunction(
                Name: InventoryToolExecutor.GetProductInventoryToolName,
                Arguments: ParseJson("""{"productId":1}""")));
        var productAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [productToolCall]);
        var inventoryAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [inventoryToolCall]);
        var finalAssistantMessage = new OllamaChatMessage(
            "assistant",
            "Demo Product Alpha stokta bulunuyor.");
        var chatClient = new RecordingOllamaChatClient(
            productAssistantMessage,
            inventoryAssistantMessage,
            finalAssistantMessage);
        ToolExecutionResult productResult = ToolExecutionResult.FromSuccess(
            new
            {
                id = 1,
                sku = "AYG-DEMO-PRD-001",
                name = "Demo Product Alpha",
                category = "DemoCategoryA",
                unitPrice = 125.50m,
                isActive = true
            });
        ToolExecutionResult inventoryResult = ToolExecutionResult.FromSuccess(
            new[]
            {
                new
                {
                    locationCode = "DEMO-LOC-01",
                    locationName = "Demo Depo Bir",
                    quantityAvailable = 120
                },
                new
                {
                    locationCode = "DEMO-LOC-02",
                    locationName = "Demo Depo Iki",
                    quantityAvailable = 35
                }
            });
        var toolExecutor = new RecordingAgentToolExecutor(
            productResult,
            inventoryResult);
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync(
            "  AYG-DEMO-PRD-001 stokta mi?  ");

        Assert.Equal("Demo Product Alpha stokta bulunuyor.", answer);
        Assert.Equal(3, chatClient.Calls.Count);
        Assert.Equal(
            new[]
            {
                ProductToolExecutor.GetProductBySkuToolName,
                InventoryToolExecutor.GetProductInventoryToolName
            },
            toolExecutor.Calls.Select(call => call.ToolName));
        Assert.Equal(
            "AYG-DEMO-PRD-001",
            toolExecutor.Calls[0].Arguments.GetProperty("sku").GetString());
        Assert.Equal(
            1,
            toolExecutor.Calls[1].Arguments.GetProperty("productId").GetInt32());

        ChatInvocation firstRequest = chatClient.Calls[0];
        Assert.Equal(
            new[] { "system", "user" },
            firstRequest.Messages.Select(message => message.Role));
        Assert.Equal("AYG-DEMO-PRD-001 stokta mi?", firstRequest.Messages[1].Content);

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal(productAssistantMessage, secondRequest.Messages[2]);
        Assert.Equal("tool", secondRequest.Messages[3].Role);
        Assert.Equal(ProductToolExecutor.GetProductBySkuToolName, secondRequest.Messages[3].ToolName);
        Assert.Equal("call-product", secondRequest.Messages[3].ToolCallId);
        Assert.Equal(productResult.Content, secondRequest.Messages[3].Content);

        ChatInvocation thirdRequest = chatClient.Calls[2];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool", "assistant", "tool" },
            thirdRequest.Messages.Select(message => message.Role));
        Assert.Equal(productAssistantMessage, thirdRequest.Messages[2]);
        Assert.Equal(secondRequest.Messages[3], thirdRequest.Messages[3]);
        Assert.Equal(inventoryAssistantMessage, thirdRequest.Messages[4]);
        Assert.Equal("tool", thirdRequest.Messages[5].Role);
        Assert.Equal(InventoryToolExecutor.GetProductInventoryToolName, thirdRequest.Messages[5].ToolName);
        Assert.Equal("call-inventory", thirdRequest.Messages[5].ToolCallId);
        Assert.Equal(inventoryResult.Content, thirdRequest.Messages[5].Content);

        Assert.All(
            chatClient.Calls,
            call =>
            {
                OllamaChatSettings settings = Assert.IsType<OllamaChatSettings>(call.Settings);
                Assert.Same(toolExecutor.ToolDefinitions, settings.Tools);
            });
    }

    [Fact]
    public async Task AskAsync_CustomerThenSales_UsesThreeModelResponsesAndExactToolHistory()
    {
        var customerToolCall = new OllamaToolCall(
            "call-customer-search",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.SearchCustomersByNameToolName,
                Arguments: ParseJson("""{"query":"Ahmet Yilmaz"}""")));
        var salesToolCall = new OllamaToolCall(
            "call-customer-sales",
            new OllamaToolCallFunction(
                Name: SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName,
                Arguments: ParseJson(
                    """{"customerId":1,"fromDate":"2025-12-07","toDate":"2026-03-06"}""")));
        var customerAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [customerToolCall]);
        var salesAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [salesToolCall]);
        var finalAssistantMessage = new OllamaChatMessage(
            "assistant",
            "Ahmet Yilmaz 3 sipariste 2941.00 TRY harcadi.");
        var chatClient = new RecordingOllamaChatClient(
            customerAssistantMessage,
            salesAssistantMessage,
            finalAssistantMessage);
        ToolExecutionResult customerResult = ToolExecutionResult.FromSuccess(
            new[]
            {
                new
                {
                    id = 1,
                    firstName = "Ahmet",
                    lastName = "Yilmaz",
                    email = "ahmet.yilmaz@example.com",
                    city = "Istanbul"
                }
            });
        ToolExecutionResult salesResult = ToolExecutionResult.FromSuccess(
            new
            {
                orderCount = 3,
                totalSpent = 2941.00m,
                itemsPurchased = 12L,
                currencyCode = "TRY"
            });
        var toolExecutor = new RecordingAgentToolExecutor(
            customerResult,
            salesResult);
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync(
            "  Ahmet Yilmaz son 90 gunde ne kadar alisveris yapti?  ");

        Assert.Equal("Ahmet Yilmaz 3 sipariste 2941.00 TRY harcadi.", answer);
        Assert.Equal(3, chatClient.Calls.Count);
        Assert.Equal(
            new[]
            {
                CustomerToolExecutor.SearchCustomersByNameToolName,
                SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName
            },
            toolExecutor.Calls.Select(call => call.ToolName));
        Assert.Equal(
            "Ahmet Yilmaz",
            toolExecutor.Calls[0].Arguments.GetProperty("query").GetString());
        JsonElement salesArguments = toolExecutor.Calls[1].Arguments;
        Assert.Equal(1, salesArguments.GetProperty("customerId").GetInt32());
        Assert.Equal("2025-12-07", salesArguments.GetProperty("fromDate").GetString());
        Assert.Equal("2026-03-06", salesArguments.GetProperty("toDate").GetString());

        ChatInvocation firstRequest = chatClient.Calls[0];
        Assert.Equal(
            new[] { "system", "user" },
            firstRequest.Messages.Select(message => message.Role));
        Assert.Equal(
            "Ahmet Yilmaz son 90 gunde ne kadar alisveris yapti?",
            firstRequest.Messages[1].Content);

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal(customerAssistantMessage, secondRequest.Messages[2]);
        Assert.Equal("tool", secondRequest.Messages[3].Role);
        Assert.Equal(
            CustomerToolExecutor.SearchCustomersByNameToolName,
            secondRequest.Messages[3].ToolName);
        Assert.Equal("call-customer-search", secondRequest.Messages[3].ToolCallId);
        Assert.Equal(customerResult.Content, secondRequest.Messages[3].Content);
        Assert.Null(secondRequest.Messages[3].ToolCalls);

        ChatInvocation thirdRequest = chatClient.Calls[2];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool", "assistant", "tool" },
            thirdRequest.Messages.Select(message => message.Role));
        Assert.Equal(customerAssistantMessage, thirdRequest.Messages[2]);
        Assert.Equal(secondRequest.Messages[3], thirdRequest.Messages[3]);
        Assert.Equal(salesAssistantMessage, thirdRequest.Messages[4]);
        Assert.Equal("tool", thirdRequest.Messages[5].Role);
        Assert.Equal(
            SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName,
            thirdRequest.Messages[5].ToolName);
        Assert.Equal("call-customer-sales", thirdRequest.Messages[5].ToolCallId);
        Assert.Equal(salesResult.Content, thirdRequest.Messages[5].Content);
        Assert.Null(thirdRequest.Messages[5].ToolCalls);

        Assert.All(
            chatClient.Calls,
            call =>
            {
                OllamaChatSettings settings = Assert.IsType<OllamaChatSettings>(call.Settings);
                Assert.Same(toolExecutor.ToolDefinitions, settings.Tools);
            });
    }

    [Fact]
    public async Task AskAsync_CustomerThenOrderThenDocuments_UsesFourModelResponsesAndExactToolHistory()
    {
        var customerToolCall = new OllamaToolCall(
            "call-customer",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.SearchCustomersByNameToolName,
                Arguments: ParseJson("""{"query":"Ahmet Yilmaz"}""")));
        var orderToolCall = new OllamaToolCall(
            "call-order",
            new OllamaToolCallFunction(
                Name: OrderToolExecutor.GetLatestCustomerOrderToolName,
                Arguments: ParseJson("""{"customerId":1}""")));
        var documentToolCall = new OllamaToolCall(
            "call-document",
            new OllamaToolCallFunction(
                Name: DocumentToolExecutor.SearchDocumentsToolName,
                Arguments: ParseJson("""{"query":"iade politikası"}""")));
        var customerAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [customerToolCall]);
        var orderAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [orderToolCall]);
        var documentAssistantMessage = new OllamaChatMessage(
            "assistant",
            null,
            [documentToolCall]);
        var finalAssistantMessage = new OllamaChatMessage(
            "assistant",
            "Son siparis AYG-DEMO-1004. Demo iade suresi 14 gundur.");
        var chatClient = new RecordingOllamaChatClient(
            customerAssistantMessage,
            orderAssistantMessage,
            documentAssistantMessage,
            finalAssistantMessage);
        ToolExecutionResult customerResult = ToolExecutionResult.FromSuccess(
            new[]
            {
                new
                {
                    id = 1,
                    firstName = "Ahmet",
                    lastName = "Yilmaz",
                    email = "ahmet.yilmaz@example.com",
                    city = "Istanbul"
                }
            });
        ToolExecutionResult orderResult = ToolExecutionResult.FromSuccess(
            new
            {
                id = 4,
                orderNumber = "AYG-DEMO-1004",
                orderDate = new DateTime(2026, 2, 22, 10, 0, 0, DateTimeKind.Utc),
                status = "Hazirlaniyor",
                totalAmount = 910.75m
            });
        ToolExecutionResult documentResult = ToolExecutionResult.FromSuccess(
            new[]
            {
                new
                {
                    documentName = "return-policy.txt",
                    text = "Demo iade suresi: Teslimattan sonra 14 gun."
                }
            });
        var toolExecutor = new RecordingAgentToolExecutor(
            customerResult,
            orderResult,
            documentResult);
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync(
            "Ahmet Yilmaz'in son siparisini kontrol et ve iade politikasini soyle.");

        Assert.Equal("Son siparis AYG-DEMO-1004. Demo iade suresi 14 gundur.", answer);
        Assert.Equal(4, chatClient.Calls.Count);
        Assert.Equal(
            new[]
            {
                CustomerToolExecutor.SearchCustomersByNameToolName,
                OrderToolExecutor.GetLatestCustomerOrderToolName,
                DocumentToolExecutor.SearchDocumentsToolName
            },
            toolExecutor.Calls.Select(call => call.ToolName));

        ChatInvocation fourthRequest = chatClient.Calls[3];
        Assert.Equal(
            new[]
            {
                "system",
                "user",
                "assistant",
                "tool",
                "assistant",
                "tool",
                "assistant",
                "tool"
            },
            fourthRequest.Messages.Select(message => message.Role));
        Assert.Equal(documentAssistantMessage, fourthRequest.Messages[6]);
        Assert.Equal("tool", fourthRequest.Messages[7].Role);
        Assert.Equal(
            DocumentToolExecutor.SearchDocumentsToolName,
            fourthRequest.Messages[7].ToolName);
        Assert.Equal("call-document", fourthRequest.Messages[7].ToolCallId);
        Assert.Equal(documentResult.Content, fourthRequest.Messages[7].Content);
    }

    [Fact]
    public async Task AskAsync_NoToolCall_ReturnsFinalContentWithoutCallingExecutor()
    {
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage("assistant", "  Merhaba! Nasıl yardımcı olabilirim?  "));
        var toolExecutor = new RecordingAgentToolExecutor();
        var agent = CreateAgent(chatClient, toolExecutor);

        string answer = await agent.AskAsync("Merhaba");

        Assert.Equal("Merhaba! Nasıl yardımcı olabilirim?", answer);
        Assert.Single(chatClient.Calls);
        Assert.Empty(toolExecutor.Calls);
        Assert.Equal(
            new[] { "system", "user" },
            chatClient.Calls[0].Messages.Select(message => message.Role));
    }

    [Fact]
    public async Task AskAsync_MultipleAllowedToolCalls_ExecutesEachAndPreservesOrder()
    {
        var firstToolCall = new OllamaToolCall(
            "call-email",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByEmailToolName,
                Arguments: ParseJson("""{"email":"ahmet.yilmaz@example.com"}""")));
        var secondToolCall = new OllamaToolCall(
            "call-id",
            new OllamaToolCallFunction(
                Name: CustomerToolExecutor.GetCustomerByIdToolName,
                Arguments: ParseJson("""{"id":1}""")));
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage("assistant", null, [firstToolCall, secondToolCall]),
            new OllamaChatMessage("assistant", "İki sorgu tamamlandı."));
        var firstResult = ToolExecutionResult.FromSuccess(new { id = 1 });
        var secondResult = ToolExecutionResult.FromSuccess(new { id = 1 });
        var toolExecutor = new RecordingAgentToolExecutor(firstResult, secondResult);
        var agent = CreateAgent(
            chatClient,
            toolExecutor,
            maxToolCallsPerIteration: 2);

        string answer = await agent.AskAsync("İki müşteri sorgusunu karşılaştır.");

        Assert.Equal("İki sorgu tamamlandı.", answer);
        Assert.Equal(2, toolExecutor.Calls.Count);
        Assert.Equal(
            new[]
            {
                CustomerToolExecutor.GetCustomerByEmailToolName,
                CustomerToolExecutor.GetCustomerByIdToolName
            },
            toolExecutor.Calls.Select(call => call.ToolName));

        ChatInvocation secondRequest = chatClient.Calls[1];
        Assert.Equal(
            new[] { "system", "user", "assistant", "tool", "tool" },
            secondRequest.Messages.Select(message => message.Role));
        Assert.Equal("call-email", secondRequest.Messages[3].ToolCallId);
        Assert.Equal("call-id", secondRequest.Messages[4].ToolCallId);
        Assert.Equal(firstResult.Content, secondRequest.Messages[3].Content);
        Assert.Equal(secondResult.Content, secondRequest.Messages[4].Content);
    }

    [Fact]
    public async Task AskAsync_RepeatedToolCalls_StopsAtConfiguredIterationLimit()
    {
        OllamaChatMessage repeatingToolCall = CreateIdToolCallAssistantMessage();
        var chatClient = new RecordingOllamaChatClient(
            repeatingToolCall,
            repeatingToolCall,
            repeatingToolCall);
        var toolExecutor = new RecordingAgentToolExecutor(
            ToolExecutionResult.FromSuccess(new { id = 1 }),
            ToolExecutionResult.FromSuccess(new { id = 1 }));
        var agent = CreateAgent(
            chatClient,
            toolExecutor,
            maxToolIterations: 2);

        AgentException exception = await Assert.ThrowsAsync<AgentException>(
            () => agent.AskAsync("Müşteri sorgusu"));

        Assert.Contains("güvenli", exception.Message);
        Assert.Contains("MaxToolIterations", exception.TechnicalDetails);
        Assert.Equal(3, chatClient.Calls.Count);
        Assert.Equal(2, toolExecutor.Calls.Count);
    }

    [Fact]
    public async Task AskAsync_TooManyToolCallsInOneResponse_RejectsBeforeExecution()
    {
        OllamaToolCall[] toolCalls = Enumerable.Range(1, 3)
            .Select(index => new OllamaToolCall(
                $"call-{index}",
                new OllamaToolCallFunction(
                    Name: CustomerToolExecutor.GetCustomerByIdToolName,
                    Arguments: ParseJson($$"""{"id":{{index}}}"""))))
            .ToArray();
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage("assistant", null, toolCalls));
        var toolExecutor = new RecordingAgentToolExecutor();
        var agent = CreateAgent(
            chatClient,
            toolExecutor,
            maxToolCallsPerIteration: 2);

        AgentException exception = await Assert.ThrowsAsync<AgentException>(
            () => agent.AskAsync("Üç müşteri sorgula"));

        Assert.Contains("güvenli", exception.Message);
        Assert.Empty(toolExecutor.Calls);
        Assert.Single(chatClient.Calls);
    }

    [Fact]
    public async Task AskAsync_ToolCallWithMissingArguments_FailsSafelyBeforeExecution()
    {
        var chatClient = new RecordingOllamaChatClient(
            new OllamaChatMessage(
                "assistant",
                null,
                [
                    new OllamaToolCall(
                        "call-invalid",
                        new OllamaToolCallFunction(
                            Name: CustomerToolExecutor.GetCustomerByEmailToolName))
                ]));
        var toolExecutor = new RecordingAgentToolExecutor();
        var agent = CreateAgent(chatClient, toolExecutor);

        AgentException exception = await Assert.ThrowsAsync<AgentException>(
            () => agent.AskAsync("Müşteri sorgusu"));

        Assert.Contains("geçerli bir tool çağrısı", exception.Message);
        Assert.Contains("arguments", exception.TechnicalDetails);
        Assert.Empty(toolExecutor.Calls);
        Assert.Single(chatClient.Calls);
    }

    private static OllamaAgentService CreateAgent(
        IOllamaChatClient chatClient,
        IAgentToolExecutor toolExecutor,
        int maxToolIterations = 5,
        int maxToolCallsPerIteration = 3)
    {
        return new OllamaAgentService(
            chatClient,
            toolExecutor,
            Options.Create(new AgentOptions
            {
                MaxToolIterations = maxToolIterations,
                MaxToolCallsPerIteration = maxToolCallsPerIteration,
                MaxConversationTurns = 4,
                MaxNameSearchResults = 5,
                MaxOrderSearchResults = 5,
                MaxProductSearchResults = 5,
                MaxInventoryLocationResults = 5
            }));
    }

    private static OllamaChatMessage CreateIdToolCallAssistantMessage()
    {
        return new OllamaChatMessage(
            "assistant",
            null,
            [
                new OllamaToolCall(
                    "call-id",
                    new OllamaToolCallFunction(
                        Name: CustomerToolExecutor.GetCustomerByIdToolName,
                        Arguments: ParseJson("""{"id":1}""")))
            ]);
    }

    private static JsonElement ParseJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class RecordingOllamaChatClient(
        params OllamaChatMessage[] responses) : IOllamaChatClient
    {
        private readonly Queue<OllamaChatMessage> _responses = new(responses);

        public List<ChatInvocation> Calls { get; } = [];

        public Task<OllamaChatMessage> ChatAsync(
            IReadOnlyCollection<OllamaChatMessage> messages,
            OllamaChatSettings? settings = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new ChatInvocation(messages.ToArray(), settings));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Test Ollama response queue is empty.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class RecordingAgentToolExecutor(
        params ToolExecutionResult[] results) : IAgentToolExecutor
    {
        private readonly Queue<ToolExecutionResult> _results = new(results);

        public IReadOnlyList<OllamaToolDefinition> ToolDefinitions { get; } =
        [
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    CustomerToolExecutor.SearchCustomersByNameToolName,
                    "Test customer search tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["query"] = new("string", "Test customer name")
                        },
                        ["query"],
                        AdditionalProperties: false))),
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    CustomerToolExecutor.GetCustomerByEmailToolName,
                    "Test tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["email"] = new("string", "Test email")
                        },
                        ["email"],
                        AdditionalProperties: false))),
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    OrderToolExecutor.GetLatestCustomerOrderToolName,
                    "Test latest order tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["customerId"] = new("integer", "Test customer id")
                        },
                        ["customerId"],
                        AdditionalProperties: false))),
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    ProductToolExecutor.GetProductBySkuToolName,
                    "Test product tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["sku"] = new("string", "Test SKU")
                        },
                        ["sku"],
                        AdditionalProperties: false))),
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    InventoryToolExecutor.GetProductInventoryToolName,
                    "Test inventory tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["productId"] = new("integer", "Test product id")
                        },
                        ["productId"],
                        AdditionalProperties: false))),
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    SalesAnalyticsToolExecutor.GetCustomerPurchaseSummaryToolName,
                    "Test customer sales tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["customerId"] = new("integer", "Test customer id"),
                            ["fromDate"] = new("string", "Test from date"),
                            ["toDate"] = new("string", "Test to date")
                        },
                        ["customerId", "fromDate", "toDate"],
                        AdditionalProperties: false))),
            new OllamaToolDefinition(
                "function",
                new OllamaToolFunctionDefinition(
                    DocumentToolExecutor.SearchDocumentsToolName,
                    "Test document search tool",
                    new OllamaToolParameters(
                        "object",
                        new Dictionary<string, OllamaToolProperty>
                        {
                            ["query"] = new("string", "Test document query")
                        },
                        ["query"],
                        AdditionalProperties: false)))
        ];

        public List<ToolInvocation> Calls { get; } = [];

        public Task<ToolExecutionResult> ExecuteAsync(
            string? toolName,
            JsonElement arguments,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement argumentsSnapshot = arguments.ValueKind == JsonValueKind.Undefined
                ? default
                : arguments.Clone();
            Calls.Add(new ToolInvocation(toolName, argumentsSnapshot));

            if (_results.Count == 0)
            {
                throw new InvalidOperationException("Test tool result queue is empty.");
            }

            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed record ChatInvocation(
        IReadOnlyList<OllamaChatMessage> Messages,
        OllamaChatSettings? Settings);

    private sealed record ToolInvocation(string? ToolName, JsonElement Arguments);
}
