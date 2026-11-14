namespace Unifesspa.UniPlus.ArchTests.Hosting;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Portal.API;

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
/// <para>Para evitar a remoção dos hosted services pelo
/// <c>DisableWolverineRuntimeForTests=true</c> do <c>ApiFactoryBase</c>, o
/// fitness usa <see cref="WebApplicationFactory{TEntryPoint}"/> direta com
/// captura do <see cref="IServiceCollection"/> via <c>ConfigureServices</c>
/// callback — sem passar pelo pipeline de remoção.</para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo público para a fixture compartilhada.")]
public sealed class MigrationBeforeWolverineRuntimeOrderTests : IClassFixture<MigrationOrderFixture>
{
    private readonly MigrationOrderFixture _fixture;

    public MigrationBeforeWolverineRuntimeOrderTests(MigrationOrderFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> EntryPointKeys =>
        new(MigrationOrderFixture.RegisteredKeys);

    /// <summary>
    /// DbContexts cuja migration on startup CADA entry point deve registrar. O
    /// host UniPlus co-hospeda os módulos internos num único contexto; o Portal
    /// é autônomo com o seu. Travar o conjunto exato impede que um entry point
    /// perca silenciosamente o seu <c>AddDbContextMigrationsOnStartup</c> (a
    /// ordem sozinha não pegaria isso).
    /// </summary>
    private static readonly Dictionary<string, string[]> MigrationContextsEsperados = new(StringComparer.Ordinal)
    {
        [MigrationOrderFixture.UniPlusKey] = ["MonolitoDbContext"],
        [MigrationOrderFixture.PortalKey] = ["PortalDbContext"],
    };

    [Theory(DisplayName = "MigrationHostedService precede WolverineRuntime no IServiceCollection (2 entry points executáveis)")]
    [MemberData(nameof(EntryPointKeys))]
    public void MigrationRegistradaAntesDeWolverineRuntime(string entryPointKey)
    {
        IReadOnlyList<ServiceDescriptor> snapshot = _fixture.GetCapturedSnapshot(entryPointKey);

        List<ServiceDescriptor> hostedServices = [..
            snapshot.Where(d => d.ServiceType == typeof(IHostedService))];

        // Cobertura: todo DbContext esperado para este entry point precisa de um
        // MigrationHostedService<TContext>. Travado ANTES da ordem para que perder
        // o registro falhe com mensagem direcional (e não apenas afrouxe
        // silenciosamente a invariante #419).
        string[] contextsRegistrados = [.. hostedServices
            .Select(d => MigrationOrderHeuristics.GetMigrationContextName(d.ImplementationType))
            .Where(nome => nome is not null)
            .Select(nome => nome!)];

        contextsRegistrados.Should().BeEquivalentTo(
            MigrationContextsEsperados[entryPointKey],
            because: "o entry point " + entryPointKey + " deve registrar AddDbContextMigrationsOnStartup<TContext> "
                + "para exatamente estes DbContexts (uniplus-api#419). No host UniPlus, o AddMonolitoPersistencia contribui o "
                + "MigrationHostedService do contexto único — se ele sumir, o schema dos módulos não é aplicado antes do Wolverine.");

        int wolverineIndex = hostedServices.FindIndex(MigrationOrderHeuristics.IsWolverineRuntime);
        // FindLastIndex: a invariante exige que TODAS as migrations do entry point
        // precedam o Wolverine — basta a última preceder.
        int ultimaMigrationIndex = hostedServices.FindLastIndex(d =>
            MigrationOrderHeuristics.IsMigrationHostedService(d.ImplementationType));

        wolverineIndex.Should().BeGreaterThanOrEqualTo(0,
            "WolverineRuntime IHostedService precisa estar registrado no Program.cs do entry point " + entryPointKey);
        ultimaMigrationIndex.Should().BeLessThan(
            wolverineIndex,
            because: "Schema EF do domínio precisa estar aplicado antes do WolverineRuntime aceitar envelopes "
                + "que toquem tabelas do módulo (uniplus-api#419). No entry point "
                + entryPointKey + ", AddDbContextMigrationsOnStartup<TContext>() precisa preceder UseWolverineOutboxCascading "
                + "(no host UniPlus, o AddMonolitoPersistencia precede o UseWolverineOutboxCascading consolidado).");
    }

    [Theory(DisplayName = "HostOptions.ServicesStartConcurrently permanece false (premissa da ordem)")]
    [MemberData(nameof(EntryPointKeys))]
    public void HostOptions_ServicesStartConcurrently_FicaFalse(string entryPointKey)
    {
        IServiceProvider provider = _fixture.GetCapturedServiceProvider(entryPointKey);

        HostOptions hostOptions = provider.GetRequiredService<IOptions<HostOptions>>().Value;

        hostOptions.ServicesStartConcurrently.Should().BeFalse(
            because: "MigrationBeforeWolverineRuntimeOrderTests usa ordem de registro do IServiceCollection "
                + "como proxy de ordem de execução dos IHostedService. Isso só vale se "
                + "HostOptions.ServicesStartConcurrently=false (default do .NET host). "
                + "Se ligado, MigrationHostedService e WolverineRuntime começam em paralelo "
                + "e a invariante de #419 não é mais garantida pela ordem de registro.");
    }
}

/// <summary>
/// Fixture xUnit que materializa as 2 <see cref="WebApplicationFactory{T}"/>
/// (uma por entry point executável: UniPlus/Portal) uma única vez por test
/// class, preservando o custo de inicialização do host. As factories são
/// <c>IDisposable</c> via composição — não há herança custom (evita pattern
/// Dispose(bool) sobre tipo abstrato).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IClassFixture<T> exige tipo público para a fixture compartilhada.")]
public sealed class MigrationOrderFixture : IDisposable
{
    public const string UniPlusKey = "UniPlus";
    public const string PortalKey = "Portal";

    public static IReadOnlyCollection<string> RegisteredKeys { get; } =
        [UniPlusKey, PortalKey];

    /// <summary>
    /// Env vars sintéticas aplicadas process-wide via static ctor. Replica o
    /// pattern do <c>ApiFactoryBase</c> (observabilidade) e dos sentinelas que
    /// usam <c>Host.CreateDefaultBuilder</c> — em minimal API, overrides via
    /// <c>ConfigureAppConfiguration</c> chegam tarde demais.
    /// </summary>
    static MigrationOrderFixture()
    {
        // Connection strings sintéticas — UseWolverineOutboxCascading e
        // AddDbContextMigrationsOnStartup registram services lazy; a conexão
        // só seria tentada no IHostedService.StartAsync, que este teste não
        // dispara (apenas Build do host para capturar IServiceCollection).
        // O host UniPlus lê UniPlusDb (contexto único dos módulos e outbox consolidado).
        // O Portal lê a sua própria.
        const string fake = "Host=fitness-not-real;Database=fake;Username=u;Password=p";
        Environment.SetEnvironmentVariable("ConnectionStrings__UniPlusDb", fake);
        Environment.SetEnvironmentVariable("ConnectionStrings__PortalDb", fake);

        // Desliga Kafka — sem isto Wolverine tenta iniciar transporte.
        Environment.SetEnvironmentVariable("Kafka__BootstrapServers", " ");

        // O registro de acesso exige o próprio Kafka para o host subir; o produtor não conecta ao ser construído.
        Environment.SetEnvironmentVariable("RegistroDeAcesso__Kafka__BootstrapServers", "localhost:9092");

        // OpenTelemetry exporter em loop contra localhost:4317 polui logs do
        // CI mesmo sem afetar correção. Desliga consistente com ApiFactoryBase.
        Environment.SetEnvironmentVariable("Observability__Enabled", "false");
    }

    private readonly CapturingFactory<HostAssemblyMarker> _uniplusFactory = new();
    private readonly CapturingFactory<PortalApiAssemblyMarker> _portalFactory = new();

    public IReadOnlyList<ServiceDescriptor> GetCapturedSnapshot(string entryPointKey) => entryPointKey switch
    {
        UniPlusKey => _uniplusFactory.CapturedSnapshot,
        PortalKey => _portalFactory.CapturedSnapshot,
        _ => throw new ArgumentOutOfRangeException(nameof(entryPointKey)),
    };

    public IServiceProvider GetCapturedServiceProvider(string entryPointKey) => entryPointKey switch
    {
        UniPlusKey => _uniplusFactory.Services,
        PortalKey => _portalFactory.Services,
        _ => throw new ArgumentOutOfRangeException(nameof(entryPointKey)),
    };

    public void Dispose()
    {
        _uniplusFactory.Dispose();
        _portalFactory.Dispose();
    }
}

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> mínima que captura o
/// <see cref="IServiceCollection"/> tal como o Program.cs do entry point o deixa,
/// antes de qualquer <c>ConfigureTestServices</c> de teste — preserva a ordem
/// de registro dos <see cref="IHostedService"/>.
/// </summary>
internal sealed class CapturingFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private IReadOnlyList<ServiceDescriptor>? _capturedSnapshot;

    /// <summary>
    /// Snapshot imutável da <see cref="IServiceCollection"/> capturado em
    /// <c>ConfigureWebHost.ConfigureServices</c> (ANTES de
    /// <c>ConfigureTestServices</c> remover os hosted services problemáticos
    /// para permitir Start). Preserva a ordem de registro original do
    /// Program.cs do módulo.
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> CapturedSnapshot =>
        _capturedSnapshot ?? Materialize();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Snapshot ANTES de ConfigureTestServices — preserva ordem original.
            _capturedSnapshot = [.. services];
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove MigrationHostedService e WolverineRuntime para permitir
            // Start do host sem tentar conectar contra Postgres sintético.
            // O snapshot acima já guardou a ordem original — esta remoção só
            // afeta o IServiceProvider materializado por get_Services.
            ServiceDescriptor[] toRemove = [.. services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && (MigrationOrderHeuristics.IsMigrationHostedService(d.ImplementationType)
                        || MigrationOrderHeuristics.IsWolverineRuntime(d)))];
            foreach (ServiceDescriptor descriptor in toRemove)
            {
                services.Remove(descriptor);
            }
        });
    }

    private IReadOnlyList<ServiceDescriptor> Materialize()
    {
        // Acessar Services força WebApplicationFactory a executar ConfigureWebHost
        // e Start do host (com os hosted services problemáticos removidos).
        // Após isso _capturedSnapshot estará preenchido pelo callback acima.
        _ = Services;
        return _capturedSnapshot
            ?? throw new InvalidOperationException(
                "ConfigureWebHost executou sem preencher _capturedSnapshot — pipeline mudou.");
    }
}

internal static class MigrationOrderHeuristics
{
    private const string MigrationHostedServiceFullName =
        "Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection.MigrationHostedService`1";

    private const string WolverineAssemblyName = "Wolverine";

    public static bool IsMigrationHostedService(Type? implementationType) =>
        implementationType is { IsGenericType: true }
        && string.Equals(
            implementationType.GetGenericTypeDefinition().FullName,
            MigrationHostedServiceFullName,
            StringComparison.Ordinal);

    /// <summary>
    /// Nome do <c>TContext</c> em <c>MigrationHostedService&lt;TContext&gt;</c>
    /// (ex.: <c>UniPlusDbContext</c>), ou <c>null</c> se o descriptor não for um
    /// migration hosted service. Usado para travar a cobertura por DbContext.
    /// </summary>
    public static string? GetMigrationContextName(Type? implementationType) =>
        IsMigrationHostedService(implementationType)
            ? implementationType!.GetGenericArguments()[0].Name
            : null;

    public static bool IsWolverineRuntime(ServiceDescriptor descriptor) =>
        descriptor.ImplementationFactory is not null
        && string.Equals(
            descriptor.ImplementationFactory.Method.DeclaringType?.Assembly.GetName().Name,
            WolverineAssemblyName,
            StringComparison.Ordinal);
}
