using Aygaz.ECommerce.Agent.Guardrails;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aygaz.ECommerce.Agent.Configuration;

public static class DomainGuardrailServiceCollectionExtensions
{
    public static IServiceCollection AddDomainGuardrails(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DomainGuardrailOptions>()
            .Bind(configuration.GetRequiredSection(DomainGuardrailOptions.SectionName))
            .Validate(
                options => IsValidPolicyValue(options.Domain),
                "DomainGuardrail:Domain boş, aşırı uzun veya wildcard olamaz.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "DomainGuardrail:Model boş olamaz.")
            .Validate(
                options => IsValidPolicyList(options.AllowedOrganizations, 10),
                "DomainGuardrail:AllowedOrganizations 1-10 geçerli değer içermelidir.")
            .Validate(
                options => IsValidPolicyList(options.AllowedCapabilities, 20),
                "DomainGuardrail:AllowedCapabilities 1-20 geçerli değer içermelidir.")
            .Validate(
                options => options.MaxInputCharacters is >= 1 and <= 4000,
                "DomainGuardrail:MaxInputCharacters 1 ile 4000 arasında olmalıdır.")
            .Validate(
                options => options.ClassifierMaxOutputTokens is >= 8 and <= 128,
                "DomainGuardrail:ClassifierMaxOutputTokens 8 ile 128 arasında olmalıdır.");

        services.AddScoped<IDomainGuardrailLogger, ConsoleDomainGuardrailLogger>();
        services.AddScoped<IDomainGuardrailService, DomainGuardrailService>();

        return services;
    }

    private static bool IsValidPolicyList(string[]? values, int maximumCount)
    {
        return values is { Length: > 0 }
            && values.Length <= maximumCount
            && values.All(IsValidPolicyValue)
            && values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Length;
    }

    private static bool IsValidPolicyValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Trim().Length <= 100
            && !value.Contains('*');
    }
}
