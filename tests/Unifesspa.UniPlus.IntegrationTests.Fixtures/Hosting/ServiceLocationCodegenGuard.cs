namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Model;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Verifica a política de service location nas chains de commands e queries do host.
/// Inicializa a geração de código sem invocar os handlers; outras falhas de geração
/// são propagadas ao chamador.
/// </summary>
public static class ServiceLocationCodegenGuard
{
    /// <summary>Retorna a política de service location efetiva no runtime Wolverine.</summary>
    public static ServiceLocationPolicy PoliticaEfetiva(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.GetRequiredService<IWolverineRuntime>().Options.ServiceLocationPolicy;
    }

    /// <summary>Verifica as chains CQRS e devolve as que exigem service location.</summary>
    public static ServiceLocationVarredura Varrer(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        HandlerGraph grafo = services.GetRequiredService<HandlerGraph>();
        HandlerChain[] cadeias = [.. grafo.AllChains().Where(MessagingMiddlewarePolicies.IsCommandOrQueryChain)];
        string[] ofensores = [.. cadeias.Select(c => ExigenciaDeServiceLocation(c, grafo, services)).OfType<string>()];
        return new ServiceLocationVarredura(cadeias.Length, ofensores);
    }

    private static string? ExigenciaDeServiceLocation(HandlerChain cadeia, HandlerGraph grafo, IServiceProvider services)
    {
        try
        {
            cadeia.InitializeSynchronously(grafo.Rules, grafo, services);
            return null;
        }
        catch (InvalidServiceLocationException ex)
        {
            return $"{cadeia.MessageType.FullName}: {ex.Message}";
        }
    }
}

/// <summary>Quantidade de chains verificadas e mensagens que exigem service location.</summary>
public sealed record ServiceLocationVarredura(int Verificadas, IReadOnlyList<string> Ofensores);
