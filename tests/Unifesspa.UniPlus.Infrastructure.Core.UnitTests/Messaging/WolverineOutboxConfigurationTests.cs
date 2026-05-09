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

    [Fact]
    public void UseWolverineOutboxCascading_KafkaConfigSectionComPath_DeveLancarArgumentException()
    {
        // Guarda contra regressão da assinatura legada (kafkaConfigKey="Kafka:BootstrapServers").
        // Bind silencioso de chave completa como seção devolveria KafkaSettings vazio e
        // desligaria Kafka sem aviso — preferimos failure clara.
        IHostBuilder host = Host.CreateDefaultBuilder();
        IConfiguration cfg = new ConfigurationBuilder().Build();

        Action acao = () => host.UseWolverineOutboxCascading(
            cfg,
            connectionStringName: "PortalDb",
            kafkaConfigSection: "Kafka:BootstrapServers");

        acao.Should().Throw<ArgumentException>()
            .WithMessage("*kafkaConfigSection*caminho de chave*");
    }
}
