namespace Unifesspa.UniPlus.Discentes.API;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.API.Errors;
using Unifesspa.UniPlus.Discentes.Infrastructure;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registro self-describing do módulo Discentes para o composition root do
/// monólito modular. Nesta fase (réplica de dados, sem endpoints públicos) registra
/// Infrastructure e o cliente de leitura do SIGAA — OpenAPI,
/// HATEOAS, Idempotency-Key e Application entram quando o módulo ganhar
/// endpoints/handlers (tasks seguintes de sincronização SIGAA).
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

        // Sem este registro, um erro de domínio do módulo que alcance uma resposta HTTP
        // sai como falha genérica, sem identificar a causa.
        services.AddSingleton<IDomainErrorRegistration, DiscentesDomainErrorRegistration>();

        services.AddDiscentesInfrastructure();

        // Leitura dos vínculos na origem. Só se registra com endereço configurado; sem
        // ele, o módulo continua subindo com a réplica e sem a integração.
        services.AddSigaaVinculoDiscenteClient(configuration);

        return services;
    }
}
