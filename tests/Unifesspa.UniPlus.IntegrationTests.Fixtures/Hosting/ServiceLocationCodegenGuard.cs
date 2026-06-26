namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using JasperFx.CodeGeneration.Model;

using Microsoft.Extensions.DependencyInjection;

using Wolverine;
using Wolverine.Runtime;

/// <summary>
/// Guarda compartilhada da política de service location do codegen Wolverine
/// (ADR-0098). Reusada pelas suítes de integração de cada host (monólito, Geo)
/// para travar o forward-compat com o <see cref="ServiceLocationPolicy.NotAllowed"/>
/// que vira default no Wolverine 6.0.
/// </summary>
/// <remarks>
/// <para>Força a geração de código de toda chain CQRS (<c>ICommand&lt;&gt;</c>/
/// <c>IQuery&lt;&gt;</c>) do host — onde vivem as dependências que podem ser opacas
/// ao codegen; os handlers de evento/smoke compartilhados injetam apenas
/// <c>ILogger</c>. A geração é lazy no Wolverine 5.39.5 (compila no 1º uso da chain);
/// a guarda dispara invocando cada mensagem com instância não-inicializada — a
/// <c>InvalidServiceLocationException</c> é lançada na geração, antes do corpo do
/// handler. Falhas de outra natureza (validação, corpo tocando o banco com dados
/// default) são ruído e ignoradas — o banco efêmero da fixture absorve qualquer
/// efeito colateral.</para>
/// <para>Desacopla a guarda dos internals do Wolverine (a <c>HandlerGraph</c> é
/// interna): a descoberta das chains é por reflexão sobre os marcadores CQRS do
/// próprio produto.</para>
/// </remarks>
public static class ServiceLocationCodegenGuard
{
    /// <summary>
    /// Política de service location efetiva no runtime Wolverine do host.
    /// </summary>
    public static ServiceLocationPolicy PoliticaEfetiva(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.GetRequiredService<IWolverineRuntime>().Options.ServiceLocationPolicy;
    }

    /// <summary>
    /// Varre as chains CQRS dos assemblies cujo nome começa com
    /// <paramref name="prefixoAssembly"/> e devolve os tipos que exigem service
    /// location sob a política vigente.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A guarda só se importa com InvalidServiceLocationException; qualquer "
            + "outra exceção (validação / corpo do handler com dados default) é ruído e ignorada.")]
    public static async Task<ServiceLocationVarredura> VarrerAsync(IServiceProvider services, string prefixoAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixoAssembly);

        using IServiceScope scope = services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        IReadOnlyList<Type> mensagens = DescobrirMensagensCqrs(prefixoAssembly);
        List<string> ofensores = [];

        foreach (Type mt in mensagens)
        {
            object instancia = RuntimeHelpers.GetUninitializedObject(mt);
            try
            {
                await bus.InvokeAsync(instancia, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                string? svcLoc = MensagemDeServiceLocation(ex);
                if (svcLoc is not null)
                {
                    ofensores.Add($"{mt.FullName}: {svcLoc}");
                }
            }
        }

        return new ServiceLocationVarredura(mensagens.Count, ofensores);
    }

    private static IReadOnlyList<Type> DescobrirMensagensCqrs(string prefixoAssembly)
    {
        Type command = typeof(Unifesspa.UniPlus.Application.Abstractions.Messaging.ICommand<>);
        Type query = typeof(Unifesspa.UniPlus.Application.Abstractions.Messaging.IQuery<>);

        return
        [
            .. AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith(prefixoAssembly, StringComparison.Ordinal) == true)
                .SelectMany(TiposCarregaveis)
                .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }
                    && t.GetInterfaces().Any(i => i.IsGenericType
                        && (i.GetGenericTypeDefinition() == command || i.GetGenericTypeDefinition() == query)))
                .Distinct()
                .OrderBy(t => t.FullName, StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<Type> TiposCarregaveis(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string? MensagemDeServiceLocation(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex.GetType().Name.Contains("ServiceLocation", StringComparison.Ordinal))
            {
                return ex.Message;
            }

            ex = ex.InnerException;
        }

        return null;
    }
}

/// <summary>
/// Resultado da varredura de service location: quantas chains CQRS foram
/// verificadas e a lista (vazia quando tudo OK) de ofensores, cada um com o
/// tipo da mensagem e o trecho da exceção de service location.
/// </summary>
public sealed record ServiceLocationVarredura(int Verificadas, IReadOnlyList<string> Ofensores);
