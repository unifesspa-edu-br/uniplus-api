namespace Unifesspa.UniPlus.Contratos.UnitTests.Hosting;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Portal.API;

/// <summary>
/// O host do monólito e o do Portal, iniciados uma só vez por processo de teste e compartilhados por todas as classes
/// que os inspecionam: cada início custa a composição completa de um executável, e classes de teste em paralelo o
/// repetiriam. Um início que falha fica registrado e reprova, com o mesmo erro, todo teste que depende do executável.
/// </summary>
internal static class HostsDoProdutoIniciados
{
    public const string UniPlus = "UniPlus";
    public const string Portal = "Portal";

    public static IReadOnlyCollection<string> Executaveis { get; } = [UniPlus, Portal];

    private static readonly Lazy<HostIniciado> HostDoUniPlus =
        new(Iniciar<HostAssemblyMarker>, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<HostIniciado> HostDoPortal =
        new(Iniciar<PortalApiAssemblyMarker>, LazyThreadSafetyMode.ExecutionAndPublication);

    // Variáveis de ambiente do processo, e não callbacks de configuração: em minimal API, o override via
    // ConfigureAppConfiguration chega tarde demais. Valem antes de qualquer host ser composto.
    static HostsDoProdutoIniciados()
    {
        DefinirConnectionStringsSinteticas();
        DefinirIntegracoesExternas();
    }

    public static HostIniciado Obter(string executavel) => executavel switch
    {
        UniPlus => HostDoUniPlus.Value,
        Portal => HostDoPortal.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(executavel)),
    };

    private static void DefinirConnectionStringsSinteticas()
    {
        // A fábrica remove a migration e o Wolverine, que conectariam no banco, antes de iniciar o host.
        // O host UniPlus lê UniPlusDb (contexto único dos módulos e outbox consolidado); o Portal lê a sua própria.
        const string fake = "Host=fitness-not-real;Database=fake;Username=u;Password=p";
        Environment.SetEnvironmentVariable("ConnectionStrings__UniPlusDb", fake);
        Environment.SetEnvironmentVariable("ConnectionStrings__PortalDb", fake);
    }

    private static void DefinirIntegracoesExternas()
    {
        // Desliga Kafka — sem isto Wolverine tenta iniciar transporte.
        Environment.SetEnvironmentVariable("Kafka__BootstrapServers", " ");

        // O registro de acesso exige o próprio Kafka para o host subir; o produtor não conecta ao ser construído.
        Environment.SetEnvironmentVariable("RegistroDeAcesso__Kafka__BootstrapServers", "localhost:9092");

        // O valor do registro de acesso é Avro, e o host exige o Schema Registry para subir; o cliente não conecta ao ser construído.
        Environment.SetEnvironmentVariable("SchemaRegistry__Url", "http://localhost:8081/apis/ccompat/v7");

        // OpenTelemetry exporter em loop contra localhost:4317 polui logs do
        // CI mesmo sem afetar correção. Desliga consistente com ApiFactoryBase.
        Environment.SetEnvironmentVariable("Observability__Enabled", "false");
    }

    // Sem fixture do xUnit não há fim de coleção para descartar a fábrica; o fim do processo de teste a descarta.
    private static HostIniciado Iniciar<TEntryPoint>()
        where TEntryPoint : class
    {
        CapturingFactory<TEntryPoint> fabrica = new();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => fabrica.Dispose();

        return new HostIniciado(fabrica.CapturedSnapshot, fabrica.Services);
    }
}

/// <summary>Um executável do produto já iniciado, com a composição de serviços que o <c>Program</c> declarou.</summary>
/// <param name="RegistrosDeServico">
/// Os registros da <see cref="IServiceCollection"/> na ordem em que o <c>Program</c> os declarou, antes de a fábrica
/// remover a migration e o runtime do Wolverine.
/// </param>
/// <param name="ProvedorDeServicos">O provedor do host iniciado, já sem a migration e sem o runtime do Wolverine.</param>
internal sealed record HostIniciado(
    IReadOnlyList<ServiceDescriptor> RegistrosDeServico,
    IServiceProvider ProvedorDeServicos);
