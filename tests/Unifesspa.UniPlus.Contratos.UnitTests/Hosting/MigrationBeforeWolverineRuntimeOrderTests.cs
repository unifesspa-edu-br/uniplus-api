namespace Unifesspa.UniPlus.Contratos.UnitTests.Hosting;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Fitness test (uniplus-api#419) que trava a invariante de ordem dos
/// <see cref="IHostedService"/> nos 2 entry points executáveis
/// (<b>UniPlus</b> — o host do monólito — e <b>Portal</b>):
/// <c>MigrationHostedService&lt;TContext&gt;</c> precisa ser registrado antes
/// do <c>WolverineRuntime</c> para que o schema EF do domínio esteja aplicado
/// quando o Wolverine começar a processar envelopes que tocam tabelas do módulo.
/// </summary>
/// <remarks>
/// <para>Os módulos internos são class libraries — não têm <c>Program</c> próprio. A ordem
/// migrations→Wolverine deles é governada por um <b>único</b> entry point, o host UniPlus, que
/// registra um só <c>MigrationHostedService</c> (o do contexto único dos módulos) via
/// <c>AddMonolitoPersistencia</c> e só então o <c>WolverineRuntime</c> consolidado. O Portal
/// permanece deployable autônomo.</para>
///
/// <para>A invariante depende de duas premissas conjuntas:
/// (1) ordem de registro no <see cref="IServiceCollection"/> determina ordem
/// de Start dos <see cref="IHostedService"/>, e
/// (2) <see cref="HostOptions.ServicesStartConcurrently"/> permanece <c>false</c>.
/// O teste verifica ambas explicitamente — sem o segundo assert, uma mudança
/// futura em <see cref="HostOptions"/> tornaria a ordem de registro insuficiente.</para>
///
/// <para>Heurísticas usadas para identificar os hosted services replicam as
/// queries de <c>ApiFactoryBase</c> (filtro de Migration) e
/// <c>WolverineRuntimeRemovalSentinelTests</c> (filtro de Wolverine).
/// Refactor para helper compartilhado é follow-up — não consolidado aqui
/// para manter o PR focado.</para>
///
/// <para>A fábrica compartilhada captura o <see cref="IServiceCollection"/> antes
/// de remover os serviços que conectariam ao banco durante a inicialização.</para>
/// </remarks>
public sealed class MigrationBeforeWolverineRuntimeOrderTests
{
    public static TheoryData<string> EntryPointKeys =>
        new(HostsDoProdutoIniciados.Executaveis);

    /// <summary>
    /// DbContexts cuja migration on startup CADA entry point deve registrar. O
    /// host UniPlus co-hospeda os módulos internos num único contexto; o Portal
    /// é autônomo com o seu. Travar o conjunto exato impede que um entry point
    /// perca silenciosamente o seu <c>AddDbContextMigrationsOnStartup</c> (a
    /// ordem sozinha não pegaria isso).
    /// </summary>
    private static readonly Dictionary<string, string[]> MigrationContextsEsperados = new(StringComparer.Ordinal)
    {
        [HostsDoProdutoIniciados.UniPlus] = ["MonolitoDbContext"],
        [HostsDoProdutoIniciados.Portal] = ["PortalDbContext"],
    };

    [Theory(DisplayName = "MigrationHostedService precede WolverineRuntime no IServiceCollection (2 entry points executáveis)")]
    [MemberData(nameof(EntryPointKeys))]
    public void MigrationRegistradaAntesDeWolverineRuntime(string entryPointKey)
    {
        List<ServiceDescriptor> hostedServices = [..
            HostsDoProdutoIniciados.Obter(entryPointKey).RegistrosDeServico.Where(d => d.ServiceType == typeof(IHostedService))];

        ConferirQueCadaContextoEsperadoTemMigration(hostedServices, entryPointKey);
        int wolverineIndex = ConferirQueOWolverineEstaRegistrado(hostedServices, entryPointKey);
        ConferirQueAsMigrationsPrecedemOWolverine(hostedServices, wolverineIndex, entryPointKey);
    }

    [Theory(DisplayName = "HostOptions.ServicesStartConcurrently permanece false (premissa da ordem)")]
    [MemberData(nameof(EntryPointKeys))]
    public void HostOptions_ServicesStartConcurrently_FicaFalse(string entryPointKey)
    {
        IServiceProvider provider = HostsDoProdutoIniciados.Obter(entryPointKey).ProvedorDeServicos;

        HostOptions hostOptions = provider.GetRequiredService<IOptions<HostOptions>>().Value;

        hostOptions.ServicesStartConcurrently.Should().BeFalse(
            because: "MigrationBeforeWolverineRuntimeOrderTests usa ordem de registro do IServiceCollection "
                + "como proxy de ordem de execução dos IHostedService. Isso só vale se "
                + "HostOptions.ServicesStartConcurrently=false (default do .NET host). "
                + "Se ligado, MigrationHostedService e WolverineRuntime começam em paralelo "
                + "e a invariante de #419 não é mais garantida pela ordem de registro.");
    }

    // Cobertura: todo DbContext esperado para este entry point precisa de um
    // MigrationHostedService<TContext>. Travado ANTES da ordem para que perder
    // o registro falhe com mensagem direcional (e não apenas afrouxe
    // silenciosamente a invariante #419).
    private static void ConferirQueCadaContextoEsperadoTemMigration(List<ServiceDescriptor> hostedServices, string entryPointKey)
    {
        string[] contextsRegistrados = [.. hostedServices
            .Select(d => MigrationOrderHeuristics.GetMigrationContextName(d.ImplementationType))
            .OfType<string>()];

        contextsRegistrados.Should().BeEquivalentTo(
            MigrationContextsEsperados[entryPointKey],
            because: "o entry point " + entryPointKey + " deve registrar AddDbContextMigrationsOnStartup<TContext> "
                + "para exatamente estes DbContexts (uniplus-api#419). No host UniPlus, o AddMonolitoPersistencia contribui o "
                + "MigrationHostedService do contexto único — se ele sumir, o schema dos módulos não é aplicado antes do Wolverine.");
    }

    private static int ConferirQueOWolverineEstaRegistrado(List<ServiceDescriptor> hostedServices, string entryPointKey)
    {
        int wolverineIndex = hostedServices.FindIndex(MigrationOrderHeuristics.IsWolverineRuntime);

        wolverineIndex.Should().BeGreaterThanOrEqualTo(0,
            "WolverineRuntime IHostedService precisa estar registrado no Program.cs do entry point " + entryPointKey);

        return wolverineIndex;
    }

    // FindLastIndex: a invariante exige que TODAS as migrations do entry point
    // precedam o Wolverine — basta a última preceder.
    private static void ConferirQueAsMigrationsPrecedemOWolverine(List<ServiceDescriptor> hostedServices, int wolverineIndex, string entryPointKey)
    {
        int ultimaMigrationIndex = hostedServices.FindLastIndex(d =>
            MigrationOrderHeuristics.IsMigrationHostedService(d.ImplementationType));

        ultimaMigrationIndex.Should().BeLessThan(
            wolverineIndex,
            because: "Schema EF do domínio precisa estar aplicado antes do WolverineRuntime aceitar envelopes "
                + "que toquem tabelas do módulo (uniplus-api#419). No entry point "
                + entryPointKey + ", AddDbContextMigrationsOnStartup<TContext>() precisa preceder UseWolverineOutboxCascading "
                + "(no host UniPlus, o AddMonolitoPersistencia precede o UseWolverineOutboxCascading consolidado).");
    }
}
