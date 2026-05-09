namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>
/// Cobertura de unit-test do helper de configuração Wolverine — escopo restrito ao que
/// dá para validar sem subir Postgres/Kafka reais. Testes E2E vivem em
/// <c>Selecao.IntegrationTests/Outbox/Cascading/</c>.
/// </summary>
public sealed class WolverineOutboxConfigurationTests
{
    [Fact]
    public void RequiresClientConfig_SoBootstrap_DeveSerFalse()
    {
        // Caminho dev/CI legado: docker-compose com PLAINTEXT, sem nenhum override.
        // Não pode disparar callback de ConfigureClient — manter exatamente o
        // comportamento pré-#343.
        KafkaSettings settings = new() { BootstrapServers = "kafka:9092" };
        WolverineOutboxConfiguration.RequiresClientConfig(settings).Should().BeFalse();
    }

    [Fact]
    public void RequiresClientConfig_VazioCompleto_DeveSerFalse()
    {
        KafkaSettings settings = new();
        WolverineOutboxConfiguration.RequiresClientConfig(settings).Should().BeFalse();
    }

    [Fact]
    public void RequiresClientConfig_ComSecurityProtocol_DeveSerTrue()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
        };
        WolverineOutboxConfiguration.RequiresClientConfig(settings).Should().BeTrue();
    }

    [Fact]
    public void RequiresClientConfig_ApenasComSslCa_DeveSerTrue()
    {
        // Mesmo sem SecurityProtocol explícito, qualquer override de SSL/SASL implica que o
        // operador quer customizar o ClientConfig — encaminhar o callback.
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SslCaLocation = "/etc/ca.crt",
        };
        WolverineOutboxConfiguration.RequiresClientConfig(settings).Should().BeTrue();
    }

    [Theory]
    [InlineData("Kafka:BootstrapServers")]
    [InlineData("Messaging:Kafka:BootstrapServers")]
    [InlineData("kafka:bootstrapservers")]
    public void UseWolverineOutboxCascading_KafkaConfigSectionComFormaLegada_DeveLancarArgumentException(string legado)
    {
        // Guarda contra regressão da assinatura legada (`Kafka:BootstrapServers` era o valor da
        // const `DefaultKafkaConfigKey` antes do #343). Bind silencioso dessa chave terminal como
        // seção devolveria `KafkaSettings` vazio e desligaria Kafka sem aviso — preferimos failure
        // clara que orienta a migração.
        IHostBuilder host = Host.CreateDefaultBuilder();
        IConfiguration cfg = new ConfigurationBuilder().Build();

        Action acao = () => host.UseWolverineOutboxCascading(
            cfg,
            connectionStringName: "PortalDb",
            kafkaConfigSection: legado);

        acao.Should().Throw<ArgumentException>()
            .WithMessage("*forma legada*");
    }

    [Theory]
    [InlineData("Kafka")]
    [InlineData("Messaging:Kafka")]
    [InlineData("Modules:Selecao:Kafka")]
    public void UseWolverineOutboxCascading_KafkaConfigSectionAninhada_DeveAceitarPath(string sectionPath)
    {
        // Paths aninhados (`Messaging:Kafka`, `Modules:Selecao:Kafka`) são endereços válidos de
        // seção em `IConfiguration.GetSection` e devem passar pela guarda. O método pode falhar
        // depois por falta de connection string, mas NÃO no guard de seção.
        IHostBuilder host = Host.CreateDefaultBuilder();
        IConfiguration cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PortalDb"] = "Host=localhost;Port=5432;Database=x;Username=u;Password=p",
            })
            .Build();

        Action acao = () => host.UseWolverineOutboxCascading(
            cfg,
            connectionStringName: "PortalDb",
            kafkaConfigSection: sectionPath);

        // Não levanta a `ArgumentException` da guarda. Pode levantar outras exceções do Wolverine
        // ao build, fora do escopo deste teste — só verificamos que o guard NÃO disparou.
        acao.Should().NotThrow<ArgumentException>(because: $"path aninhado '{sectionPath}' é válido para IConfiguration.GetSection");
    }
}
