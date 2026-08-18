using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class CustomerToolExecutor : IAgentToolModule
{
    public const string GetCustomerByEmailToolName = "get_customer_by_email";
    public const string GetCustomerByIdToolName = "get_customer_by_id";
    public const string SearchCustomersByNameToolName = "search_customers_by_name";

    private const int MaximumEmailLength = 254;
    private const int MaximumNameQueryLength = 100;

    private static readonly IReadOnlyList<OllamaToolDefinition> Definitions =
        Array.AsReadOnly(new OllamaToolDefinition[]
        {
            CreateToolDefinition(
                GetCustomerByEmailToolName,
                "E-posta adresine göre tek bir müşteriyi ve müşteri ID'sini getirir.",
                "email",
                "string",
                "Aranacak müşterinin e-posta adresi."),
            CreateToolDefinition(
                GetCustomerByIdToolName,
                "Pozitif müşteri kimlik numarasına göre tek bir müşteriyi getirir.",
                "id",
                "integer",
                "Aranacak müşterinin pozitif kimlik numarası."),
            CreateToolDefinition(
                SearchCustomersByNameToolName,
                "Ad veya soyada göre müşterileri ve ID'lerini sınırlı sayıda getirir.",
                "query",
                "string",
                "Aranacak müşterinin adı, soyadı veya tam adı.")
        });

    private readonly ICustomerService _customerService;
    private readonly AgentOptions _options;
    private readonly IToolCallLogger _logger;

    public CustomerToolExecutor(
        ICustomerService customerService,
        IOptions<AgentOptions> options,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(customerService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (options.Value.MaxNameSearchResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Agent:MaxNameSearchResults sıfırdan büyük olmalıdır.");
        }

        _customerService = customerService;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<OllamaToolDefinition> ToolDefinitions => Definitions;

    public async Task<ToolExecutionResult> ExecuteAsync(
        string? toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogToolCall(toolName);

        ToolExecutionResult result = toolName switch
        {
            GetCustomerByEmailToolName =>
                await ExecuteGetByEmailAsync(arguments, cancellationToken),
            GetCustomerByIdToolName =>
                await ExecuteGetByIdAsync(arguments, cancellationToken),
            SearchCustomersByNameToolName =>
                await ExecuteSearchByNameAsync(arguments, cancellationToken),
            _ => ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.")
        };

        _logger.LogResult(result.Status);
        return result;
    }

    private async Task<ToolExecutionResult> ExecuteGetByEmailAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "email", out JsonElement emailElement)
            || emailElement.ValueKind != JsonValueKind.String)
        {
            _logger.LogArguments("email", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir e-posta adresi belirtilmedi.");
        }

        string email = emailElement.GetString()?.Trim() ?? string.Empty;
        _logger.LogArguments("email", email);

        if (!IsValidEmail(email))
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir e-posta adresi belirtilmedi.");
        }

        CustomerDto? customer = await _customerService.GetCustomerByEmailAsync(
            email,
            cancellationToken);

        return customer is null
            ? ToolExecutionResult.CustomerNotFound()
            : ToolExecutionResult.FromSuccess(ToAgentResult(customer));
    }

    private async Task<ToolExecutionResult> ExecuteGetByIdAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "id", out JsonElement idElement)
            || idElement.ValueKind != JsonValueKind.Number
            || !idElement.TryGetInt32(out int id)
            || id <= 0)
        {
            _logger.LogArguments("id", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir müşteri kimlik numarası belirtilmedi.");
        }

        _logger.LogArguments("id", id.ToString(CultureInfo.InvariantCulture));

        CustomerDto? customer = await _customerService.GetCustomerByIdAsync(
            id,
            cancellationToken);

        return customer is null
            ? ToolExecutionResult.CustomerNotFound()
            : ToolExecutionResult.FromSuccess(ToAgentResult(customer));
    }

    private async Task<ToolExecutionResult> ExecuteSearchByNameAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryGetOnlyArgument(arguments, "query", out JsonElement queryElement)
            || queryElement.ValueKind != JsonValueKind.String)
        {
            _logger.LogArguments("query", null);
            return ToolExecutionResult.Rejected(
                "Geçerli bir ad veya soyad sorgusu belirtilmedi.");
        }

        string query = queryElement.GetString()?.Trim() ?? string.Empty;
        _logger.LogArguments("query", query);

        if (query.Length == 0 || query.Length > MaximumNameQueryLength)
        {
            return ToolExecutionResult.Rejected(
                "Geçerli bir ad veya soyad sorgusu belirtilmedi.");
        }

        IReadOnlyList<CustomerDto> customers =
            await _customerService.SearchCustomersByNameAsync(query, cancellationToken);

        CustomerAgentResult[] results = customers
            .Take(_options.MaxNameSearchResults)
            .Select(ToAgentResult)
            .ToArray();

        return results.Length == 0
            ? ToolExecutionResult.CustomerNotFound()
            : ToolExecutionResult.FromSuccess(results);
    }

    private static bool TryGetOnlyArgument(
        JsonElement arguments,
        string expectedName,
        out JsonElement value)
    {
        value = default;

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        int propertyCount = 0;

        foreach (JsonProperty property in arguments.EnumerateObject())
        {
            propertyCount++;

            if (propertyCount > 1
                || !property.Name.Equals(expectedName, StringComparison.Ordinal))
            {
                return false;
            }

            value = property.Value;
        }

        return propertyCount == 1;
    }

    private static bool IsValidEmail(string email)
    {
        return email.Length is > 0 and <= MaximumEmailLength
            && MailAddress.TryCreate(email, out MailAddress? parsedAddress)
            && parsedAddress.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
    }

    private static CustomerAgentResult ToAgentResult(CustomerDto customer)
    {
        return new CustomerAgentResult(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.Email,
            customer.City);
    }

    private static OllamaToolDefinition CreateToolDefinition(
        string name,
        string description,
        string argumentName,
        string argumentType,
        string argumentDescription)
    {
        var properties = new ReadOnlyDictionary<string, OllamaToolProperty>(
            new Dictionary<string, OllamaToolProperty>(StringComparer.Ordinal)
            {
                [argumentName] = new OllamaToolProperty(
                    argumentType,
                    argumentDescription)
            });

        return new OllamaToolDefinition(
            "function",
            new OllamaToolFunctionDefinition(
                name,
                description,
                new OllamaToolParameters(
                    "object",
                    properties,
                    Array.AsReadOnly(new[] { argumentName }),
                    AdditionalProperties: false)));
    }
}
