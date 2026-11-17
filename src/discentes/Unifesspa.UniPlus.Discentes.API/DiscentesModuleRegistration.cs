namespace Unifesspa.UniPlus.Discentes.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Infrastructure;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

/// <summary>
/// Registro self-describing do módulo Discentes para o composition root do
/// monólito modular. O módulo não tem endpoints: registra a Infrastructure e o cliente
/// de leitura do SIGAA. O registro de erros, o OpenAPI, o HATEOAS e a Idempotency-Key
/// entram junto com o primeiro endpoint.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Referenciado pelo composition root (host do monólito modular) fora deste assembly.")]
public static class DiscentesModuleRegistration
{
    public static IServiceCollection AddDiscentesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDiscentesInfrastructure();

        // Leitura dos vínculos na origem. Só se registra com endereço configurado; sem
        // ele, o módulo continua subindo com a réplica e sem a integração.
        services.AddSigaaVinculoDiscenteClient(configuration);

        return services;
    }
}
