using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Aygaz.ECommerce.Agent.Configuration;
using Aygaz.ECommerce.Agent.Models;
using Aygaz.ECommerce.Agent.Models.Agent;
using Aygaz.ECommerce.Agent.Services;
using Microsoft.Extensions.Options;

namespace Aygaz.ECommerce.Agent.Tools;

public sealed class SalesAnalyticsToolExecutor : IAgentToolModule
{
    public const string GetSalesSummaryToolName = "get_sales_summary";
    public const string GetTopSellingProductsToolName =
        "get_top_selling_products";
    public const string GetCustomerPurchaseSummaryToolName =
        "get_customer_purchase_summary";

    private const string IsoDateFormat = "yyyy-MM-dd";
    private const int MaximumAllowedAnalysisRangeDays = 366;
    private const int MaximumAllowedTopProducts = 10;

    private static readonly IReadOnlyList<string> DateRangeArguments =
        Array.AsReadOnly(new[] { "fromDate", "toDate" });

    private static readonly IReadOnlyList<string> TopProductsArguments =
        Array.AsReadOnly(new[] { "fromDate", "toDate", "limit" });

    private static readonly IReadOnlyList<string> CustomerSummaryArguments =
        Array.AsReadOnly(new[] { "customerId", "fromDate", "toDate" });

    private readonly ISalesAnalyticsService _salesAnalyticsService;
    private readonly CommerceOptions _options;
    private readonly IToolCallLogger _logger;
    private readonly IReadOnlyList<OllamaToolDefinition> _toolDefinitions;

    public SalesAnalyticsToolExecutor(
        ISalesAnalyticsService salesAnalyticsService,
        IOptions<CommerceOptions> options,
        IToolCallLogger logger)
    {
        ArgumentNullException.ThrowIfNull(salesAnalyticsService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        CommerceOptions optionValues = options.Value;
        ValidateOptions(optionValues);

        DateOnly referenceDate = ParseReferenceDate(
            optionValues.AnalyticsReferenceDate);
        string referenceDateText = referenceDate.ToString(
            IsoDateFormat,
            CultureInfo.InvariantCulture);
        string relativeDateGuidance = CreateRelativeDateGuidance(
            referenceDate,
            referenceDateText);

        _salesAnalyticsService = salesAnalyticsService;
        _options = optionValues;
        _logger = logger;
        _toolDefinitions = Array.AsReadOnly(new OllamaToolDefinition[]
        {
            CreateToolDefinition(
                GetSalesSummaryToolName,
                "İptal edilmemiş sentetik siparişler için belirtilen dahil ISO tarih "
                    + "aralığındaki hesaplanmış satış özetini getirir; ham sipariş veya "
                    + $"kalem verisi döndürmez. {relativeDateGuidance}",
                ("fromDate", "string", "Dahil başlangıç tarihi; exact yyyy-MM-dd."),
                ("toDate", "string", "Dahil bitiş tarihi; exact yyyy-MM-dd.")),
            CreateToolDefinition(
                GetTopSellingProductsToolName,
                "İptal edilmemiş sentetik siparişlerden belirtilen dahil ISO tarih "
                    + "aralığındaki en çok satan ürünleri hesaplanmış aggregate olarak "
                    + $"getirir. limit en fazla {optionValues.MaximumTopProducts}. "
                    + relativeDateGuidance,
                ("fromDate", "string", "Dahil başlangıç tarihi; exact yyyy-MM-dd."),
                ("toDate", "string", "Dahil bitiş tarihi; exact yyyy-MM-dd."),
                ("limit", "integer", "Döndürülecek pozitif ürün sayısı.")),
            CreateToolDefinition(
                GetCustomerPurchaseSummaryToolName,
                "Belirtilen müşteri kimliği ve dahil ISO tarih aralığı için iptal "
                    + "edilmemiş sentetik alışverişlerin hesaplanmış özetini getirir. "
                    + "Müşteri kimliği bilinmiyorsa önce uygun customer lookup/search "
                    + "tool'unu kullan; ID bulununca ara cevap vermeden bu tool'u çağır. "
                    + relativeDateGuidance,
                ("customerId", "integer", "Müşterinin pozitif kimlik numarası."),
                ("fromDate", "string", "Dahil başlangıç tarihi; exact yyyy-MM-dd."),
                ("toDate", "string", "Dahil bitiş tarihi; exact yyyy-MM-dd."))
        });
    }

    public IReadOnlyList<OllamaToolDefinition> ToolDefinitions =>
        _toolDefinitions;

    public async Task<ToolExecutionResult> ExecuteAsync(
        string? toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogToolCall(toolName);

        ToolExecutionResult result = toolName switch
        {
            GetSalesSummaryToolName =>
                await ExecuteSalesSummaryAsync(arguments, cancellationToken),
            GetTopSellingProductsToolName =>
                await ExecuteTopSellingProductsAsync(arguments, cancellationToken),
            GetCustomerPurchaseSummaryToolName =>
                await ExecuteCustomerPurchaseSummaryAsync(
                    arguments,
                    cancellationToken),
            _ => ToolExecutionResult.Rejected("İstenen tool kullanılamıyor.")
        };

        _logger.LogResult(result.Status);
        return result;
    }

    private async Task<ToolExecutionResult> ExecuteSalesSummaryAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryReadDateRange(
                arguments,
                DateRangeArguments,
                out DateOnly fromDate,
                out DateOnly toDate,
                out _))
        {
            LogInvalidArguments(DateRangeArguments);
            return InvalidDateRangeResult();
        }

        LogDateRange(fromDate, toDate);

        SalesSummaryDto summary =
            await _salesAnalyticsService.GetSalesSummaryAsync(
                fromDate,
                toDate,
                cancellationToken);

        return ToolExecutionResult.FromSuccess(
            new SalesSummaryAgentResult(
                summary.TotalRevenue,
                summary.OrderCount,
                summary.ItemsSold,
                summary.AverageOrderValue,
                _options.CurrencyCode));
    }

    private async Task<ToolExecutionResult> ExecuteTopSellingProductsAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryReadDateRange(
                arguments,
                TopProductsArguments,
                out DateOnly fromDate,
                out DateOnly toDate,
                out IReadOnlyDictionary<string, JsonElement> values)
            || !TryReadPositiveInt32(values["limit"], out int limit)
            || limit > _options.MaximumTopProducts)
        {
            LogInvalidArguments(TopProductsArguments);
            return ToolExecutionResult.Rejected(
                "Geçerli bir tarih aralığı ve güvenli ürün limiti belirtilmedi.");
        }

        LogDateRange(fromDate, toDate);
        _logger.LogArguments(
            "limit",
            limit.ToString(CultureInfo.InvariantCulture));

        IReadOnlyList<TopSellingProductDto> products =
            await _salesAnalyticsService.GetTopSellingProductsAsync(
                fromDate,
                toDate,
                limit,
                cancellationToken);

        TopSellingProductAgentResult[] minimizedProducts = products
            .Take(limit)
            .Select(product => new TopSellingProductAgentResult(
                product.Sku,
                product.ProductName,
                product.QuantitySold,
                product.Revenue))
            .ToArray();

        return ToolExecutionResult.FromSuccess(
            new TopSellingProductsAgentResult(
                _options.CurrencyCode,
                minimizedProducts));
    }

    private async Task<ToolExecutionResult> ExecuteCustomerPurchaseSummaryAsync(
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!TryReadDateRange(
                arguments,
                CustomerSummaryArguments,
                out DateOnly fromDate,
                out DateOnly toDate,
                out IReadOnlyDictionary<string, JsonElement> values)
            || !TryReadPositiveInt32(values["customerId"], out int customerId))
        {
            LogInvalidArguments(CustomerSummaryArguments);
            return ToolExecutionResult.Rejected(
                "Geçerli bir müşteri kimliği ve tarih aralığı belirtilmedi.");
        }

        _logger.LogArguments(
            "customerId",
            customerId.ToString(CultureInfo.InvariantCulture));
        LogDateRange(fromDate, toDate);

        CustomerPurchaseSummaryDto? summary =
            await _salesAnalyticsService.GetCustomerPurchaseSummaryAsync(
                customerId,
                fromDate,
                toDate,
                cancellationToken);

        return summary is null
            ? ToolExecutionResult.CustomerNotFound()
            : ToolExecutionResult.FromSuccess(
                new CustomerPurchaseSummaryAgentResult(
                    summary.OrderCount,
                    summary.TotalSpent,
                    summary.ItemsPurchased,
                    _options.CurrencyCode));
    }

    private bool TryReadDateRange(
        JsonElement arguments,
        IReadOnlyList<string> expectedNames,
        out DateOnly fromDate,
        out DateOnly toDate,
        out IReadOnlyDictionary<string, JsonElement> values)
    {
        fromDate = default;
        toDate = default;
        values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (!TryReadExactArguments(arguments, expectedNames, out Dictionary<string, JsonElement> parsed)
            || !TryParseIsoDate(parsed["fromDate"], out fromDate)
            || !TryParseIsoDate(parsed["toDate"], out toDate)
            || fromDate > toDate
            || toDate.DayNumber - fromDate.DayNumber
                >= _options.MaximumAnalysisRangeDays)
        {
            return false;
        }

        values = parsed;
        return true;
    }

    private static bool TryReadExactArguments(
        JsonElement arguments,
        IReadOnlyList<string> expectedNames,
        out Dictionary<string, JsonElement> values)
    {
        values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (JsonProperty property in arguments.EnumerateObject())
        {
            bool isExpected = false;

            foreach (string expectedName in expectedNames)
            {
                if (property.Name.Equals(expectedName, StringComparison.Ordinal))
                {
                    isExpected = true;
                    break;
                }
            }

            if (!isExpected || !values.TryAdd(property.Name, property.Value))
            {
                return false;
            }
        }

        return values.Count == expectedNames.Count;
    }

    private static bool TryParseIsoDate(
        JsonElement value,
        out DateOnly date)
    {
        date = default;

        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? rawDate = value.GetString();
        return rawDate?.Length == IsoDateFormat.Length
            && DateOnly.TryParseExact(
                rawDate,
                IsoDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date);
    }

    private static bool TryReadPositiveInt32(
        JsonElement value,
        out int number)
    {
        number = 0;

        return value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out number)
            && number > 0;
    }

    private void LogDateRange(DateOnly fromDate, DateOnly toDate)
    {
        _logger.LogArguments(
            "fromDate",
            fromDate.ToString(IsoDateFormat, CultureInfo.InvariantCulture));
        _logger.LogArguments(
            "toDate",
            toDate.ToString(IsoDateFormat, CultureInfo.InvariantCulture));
    }

    private void LogInvalidArguments(IReadOnlyList<string> argumentNames)
    {
        foreach (string argumentName in argumentNames)
        {
            _logger.LogArguments(argumentName, null);
        }
    }

    private static ToolExecutionResult InvalidDateRangeResult()
    {
        return ToolExecutionResult.Rejected(
            "Geçerli ve güvenli bir satış analizi tarih aralığı belirtilmedi.");
    }

    private static void ValidateOptions(CommerceOptions options)
    {
        if (!IsValidCurrencyCode(options.CurrencyCode))
        {
            throw new ArgumentException(
                "Commerce:CurrencyCode üç büyük ASCII harften oluşmalıdır.",
                nameof(options));
        }

        _ = ParseReferenceDate(options.AnalyticsReferenceDate);

        if (options.MaximumAnalysisRangeDays is < 1
            or > MaximumAllowedAnalysisRangeDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Commerce:MaximumAnalysisRangeDays 1 ile 366 arasında olmalıdır.");
        }

        if (options.MaximumTopProducts is < 1 or > MaximumAllowedTopProducts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Commerce:MaximumTopProducts 1 ile 10 arasında olmalıdır.");
        }
    }

    private static DateOnly ParseReferenceDate(string? value)
    {
        if (value?.Length != IsoDateFormat.Length
            || !DateOnly.TryParseExact(
                value,
                IsoDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly referenceDate))
        {
            throw new ArgumentException(
                "Commerce:AnalyticsReferenceDate exact yyyy-MM-dd olmalıdır.",
                nameof(value));
        }

        return referenceDate;
    }

    private static bool IsValidCurrencyCode(string? value)
    {
        return value is { Length: 3 }
            && value.All(character => character is >= 'A' and <= 'Z');
    }

    private static string CreateRelativeDateGuidance(
        DateOnly referenceDate,
        string referenceDateText)
    {
        if (referenceDate.DayNumber >= 89)
        {
            string lastThirtyDaysStart = referenceDate
                .AddDays(-29)
                .ToString(IsoDateFormat, CultureInfo.InvariantCulture);
            string lastNinetyDaysStart = referenceDate
                .AddDays(-89)
                .ToString(IsoDateFormat, CultureInfo.InvariantCulture);

            return $"Göreli tarihleri güvenilir demo referans tarihi {referenceDateText} "
                + "üzerinden hesapla: son N gün için toDate referans tarihi, fromDate "
                + "referans tarihinden N-1 gün öncesidir. "
                + $"Son 30 gün dahil {lastThirtyDaysStart}..{referenceDateText}; "
                + $"son 90 gün dahil {lastNinetyDaysStart}..{referenceDateText}.";
        }

        return $"Göreli tarihleri güvenilir demo referans tarihi {referenceDateText} "
            + "üzerinden hesapla.";
    }

    private static OllamaToolDefinition CreateToolDefinition(
        string name,
        string description,
        params (string Name, string Type, string Description)[] arguments)
    {
        var properties = new Dictionary<string, OllamaToolProperty>(
            StringComparer.Ordinal);

        foreach ((string argumentName, string argumentType, string argumentDescription)
            in arguments)
        {
            properties.Add(
                argumentName,
                new OllamaToolProperty(argumentType, argumentDescription));
        }

        return new OllamaToolDefinition(
            "function",
            new OllamaToolFunctionDefinition(
                name,
                description,
                new OllamaToolParameters(
                    "object",
                    new ReadOnlyDictionary<string, OllamaToolProperty>(properties),
                    Array.AsReadOnly(arguments.Select(argument => argument.Name).ToArray()),
                    AdditionalProperties: false)));
    }
}
