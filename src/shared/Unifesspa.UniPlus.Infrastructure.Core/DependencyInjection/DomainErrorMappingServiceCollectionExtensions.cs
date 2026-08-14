namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Errors;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Registra o mapeamento <c>code → (status, type, title)</c> da ADR-0024: o registry que
/// resolve status e title a partir dos registros de cada módulo, e a fábrica única do
/// campo <c>type</c>, compartilhada com os emissores de <c>problem+json</c> que não
/// passam por erro de domínio (401/403, 406 e o boundary de exceção).
/// </summary>
public static class DomainErrorMappingServiceCollectionExtensions
{
    public static IServiceCollection AddDomainErrorMapper(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ProblemTypeOptions>()
            .Bind(configuration.GetSection(ProblemTypeOptions.SectionName))
            // ValidateOnStart: base ausente ou malformada derruba o boot. Sem isso, o
            // defeito só apareceria na primeira resposta de erro — e como o campo type
            // não é lido no caminho feliz, passaria despercebido.
            .ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<ProblemTypeOptions>, ProblemTypeOptionsValidator>();
        services.TryAddSingleton<IProblemTypeUriFactory, ProblemTypeUriFactory>();

        services.AddSingleton<IDomainErrorRegistration, KernelDomainErrorRegistration>();
        services.AddSingleton<IDomainErrorMapper>(sp =>
        {
            IEnumerable<IDomainErrorRegistration> registrations = sp.GetServices<IDomainErrorRegistration>();
            return new DomainErrorMappingRegistry(
                registrations,
                sp.GetRequiredService<IProblemTypeUriFactory>());
        });
        return services;
    }
}
