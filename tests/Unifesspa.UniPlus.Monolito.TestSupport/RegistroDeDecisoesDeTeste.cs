namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Guarda em memória as decisões de acesso que o ponto de decisão registra, para o teste conferir o que a trilha recebeu:
/// quantas decisões, de qual permissão, com qual resultado e qual sensibilidade.
/// </summary>
public sealed class RegistroDeDecisoesDeTeste : IRegistroOperacionalRestrito
{
    private readonly ConcurrentQueue<RegistroDecisaoAcesso> _registrados = new();

    public IReadOnlyList<RegistroDecisaoAcesso> Registrados => [.. _registrados];

    public void Registrar(RegistroDecisaoAcesso registro) => _registrados.Enqueue(registro);
}

/// <summary>Registro em memória das decisões de acesso nos hosts de teste.</summary>
public static class RegistroDeDecisoesDeTesteExtensions
{
    public static IServiceCollection AdicionarRegistroDeDecisoesDeTeste(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IRegistroOperacionalRestrito>();
        services.AddSingleton<RegistroDeDecisoesDeTeste>();
        services.AddSingleton<IRegistroOperacionalRestrito>(sp => sp.GetRequiredService<RegistroDeDecisoesDeTeste>());
        return services;
    }
}
