namespace Unifesspa.UniPlus.Discentes.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Domain.Interfaces;
using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence.Repositories;

/// <summary>
/// Registra a infraestrutura do módulo Discentes (repositórios). O contexto e a unidade de trabalho vêm da persistência
/// única do monólito. <see cref="IUniPlusEncryptionService"/> já é registrado globalmente
/// pelo Host (<c>AddUniPlusEncryption</c>) — este módulo só o consome via DI no
/// repositório de <c>VinculoDiscente</c> (ADR-0121).
/// </summary>
public static class DiscentesInfrastructureRegistration
{
    public static IServiceCollection AddDiscentesInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IVinculoDiscenteRepository, VinculoDiscenteRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();

        return services;
    }
}
