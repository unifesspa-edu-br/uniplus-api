namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

public sealed class HealthChecksServiceCollectionExtensionsTests
{
    private const string ConexaoDoPostgres = "Host=pg;Port=5432;Database=x;Username=u;Password=p";

    private static IConfiguration CriarConfiguracao(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static Dictionary<string, string?> ConfiguracaoCompleta() => new()
    {
        ["ConnectionStrings:PortalDb"] = ConexaoDoPostgres,
        ["Redis:ConnectionString"] = "redis:6379",
        ["Storage:Endpoint"] = "minio:9000",
        ["Kafka:BootstrapServers"] = "kafka:9092",
    };

    private static List<HealthCheckRegistration> Registrar(Dictionary<string, string?> configuracao, string nomeDaConexao = "PortalDb")
    {
        ServiceCollection services = new();
        services.AddOptions();
        services.AddUniPlusHealthChecks(CriarConfiguracao(configuracao), nomeDaConexao);

        using ServiceProvider provedor = services.BuildServiceProvider();
        return [.. provedor.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations];
    }

    [Fact]
    public void AddUniPlusHealthChecks_ServicesNulo_LancaArgumentNullException()
    {
        IServiceCollection? services = null;
        Action acao = () => services!.AddUniPlusHealthChecks(CriarConfiguracao([]), "PortalDb");
        acao.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddUniPlusHealthChecks_ConfigurationNulo_LancaArgumentNullException()
    {
        ServiceCollection services = new();
        Action acao = () => services.AddUniPlusHealthChecks(null!, "PortalDb");
        acao.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddUniPlusHealthChecks_ConnectionStringNameVazio_LancaArgumentException(string valor)
    {
        ServiceCollection services = new();
        Action acao = () => services.AddUniPlusHealthChecks(CriarConfiguracao([]), valor);
        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddUniPlusHealthChecks_NenhumaConfig_NaoRegistraNenhumCheck()
    {
        List<HealthCheckRegistration> registros = Registrar([]);

        registros.Select(registro => registro.Name).Should().BeEmpty();
    }

    [Fact]
    public void AddUniPlusHealthChecks_TodasAsConfigsPresentes_RegistraTodosOsChecks()
    {
        List<HealthCheckRegistration> registros = Registrar(ConfiguracaoCompleta());

        registros.Select(registro => registro.Name)
            .Should().Equal("postgres", "redis", "minio", "kafka");
    }

    [Fact]
    public void AddUniPlusHealthChecks_CadaCheckTagueadoComReady()
    {
        List<HealthCheckRegistration> registros = Registrar(ConfiguracaoCompleta());

        registros.Should().AllSatisfy(registro =>
            registro.Tags.Should().Contain(HealthChecksServiceCollectionExtensions.ReadyTag));
    }

    [Fact]
    public void AddUniPlusHealthChecks_ApenasPostgres_RegistraSomentePostgres()
    {
        Dictionary<string, string?> configuracao = new() { ["ConnectionStrings:SelecaoDb"] = ConexaoDoPostgres };

        List<HealthCheckRegistration> registros = Registrar(configuracao, "SelecaoDb");

        registros.Select(registro => registro.Name).Should().Equal("postgres");
    }

    [Theory]
    [InlineData("Redis:ConnectionString", "redis:6379", "redis", "cache")]
    [InlineData("Storage:Endpoint", "minio:9000", "minio", "storage")]
    [InlineData("Kafka:BootstrapServers", "kafka:9092", "kafka", "messaging")]
    public void AddUniPlusHealthChecks_ApenasUmaChave_RegistraSomenteEsseCheckComSuasTags(
        string chave, string valor, string nomeEsperado, string tagEsperada)
    {
        List<HealthCheckRegistration> registros = Registrar(new() { [chave] = valor });

        HealthCheckRegistration registro = registros.Should().ContainSingle().Subject;
        registro.Name.Should().Be(nomeEsperado);
        registro.Tags.Should().BeEquivalentTo([HealthChecksServiceCollectionExtensions.ReadyTag, tagEsperada]);
    }

    [Theory]
    [InlineData("Redis:ConnectionString", "redis")]
    [InlineData("Storage:Endpoint", "minio")]
    [InlineData("Kafka:BootstrapServers", "kafka")]
    [InlineData("ConnectionStrings:PortalDb", "postgres")]
    public void AddUniPlusHealthChecks_ChaveEmBranco_OmiteSomenteAVerificacaoCorrespondente(
        string chave, string verificacaoOmitida)
    {
        Dictionary<string, string?> configuracao = ConfiguracaoCompleta();
        configuracao[chave] = "   ";

        List<HealthCheckRegistration> registros = Registrar(configuracao);

        string[] esperadas = ["postgres", "redis", "minio", "kafka"];
        registros.Select(registro => registro.Name)
            .Should().Equal(esperadas.Where(nome => nome != verificacaoOmitida));
    }

    [Theory]
    [InlineData("Redis:ConnectionString", "redis")]
    [InlineData("Storage:Endpoint", "minio")]
    [InlineData("Kafka:BootstrapServers", "kafka")]
    [InlineData("ConnectionStrings:PortalDb", "postgres")]
    public void AddUniPlusHealthChecks_ChaveAusente_OmiteSomenteAVerificacaoCorrespondente(
        string chave, string verificacaoOmitida)
    {
        Dictionary<string, string?> configuracao = ConfiguracaoCompleta();
        configuracao.Remove(chave);

        List<HealthCheckRegistration> registros = Registrar(configuracao);

        string[] esperadas = ["postgres", "redis", "minio", "kafka"];
        registros.Select(registro => registro.Name)
            .Should().Equal(esperadas.Where(nome => nome != verificacaoOmitida));
    }

    [Fact]
    public void AddUniPlusHealthChecks_Postgres_RegistraComAsTagsDeProntidadeEBanco()
    {
        List<HealthCheckRegistration> registros = Registrar(ConfiguracaoCompleta());

        HealthCheckRegistration postgres = registros.Single(registro => registro.Name == "postgres");
        postgres.Tags.Should().BeEquivalentTo([HealthChecksServiceCollectionExtensions.ReadyTag, "db"]);
        postgres.Factory(new ServiceCollection().BuildServiceProvider()).Should().BeOfType<PostgresHealthCheck>();
    }
}
