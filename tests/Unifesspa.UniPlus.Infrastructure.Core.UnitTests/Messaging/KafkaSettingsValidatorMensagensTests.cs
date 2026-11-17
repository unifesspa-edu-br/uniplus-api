namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>Compara a lista completa e ordenada de falhas que o validador do transporte Kafka devolve.</summary>
public sealed class KafkaSettingsValidatorMensagensTests
{
    private static ValidateOptionsResult Validar(KafkaSettings configuracao, string? nome = null) =>
        new KafkaSettingsValidator().Validate(nome, configuracao);

    [Fact]
    public void Validate_BootstrapEmBrancoComCamposInvalidos_DevolveSucesso()
    {
        KafkaSettings configuracao = new() { BootstrapServers = "   ", SecurityProtocol = "MTLS", SaslUsername = "usuario-ficticio" };

        Validar(configuracao).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ProtocoloInvalidoComOutrosCamposPreenchidos_DevolveSoAFalhaDoProtocolo()
    {
        KafkaSettings configuracao = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "MTLS",
            SaslMechanism = "PLAIN",
            SslCaLocation = "/etc/ca.crt",
        };

        Validar(configuracao).Failures.Should().Equal(
            "Kafka:SecurityProtocol 'MTLS' inválido. Use PLAINTEXT, SSL, SASL_PLAINTEXT ou SASL_SSL.");
    }

    [Fact]
    public void Validate_SaslSslSemMecanismoENemCa_DevolveMecanismoDepoisCa()
    {
        KafkaSettings configuracao = new() { BootstrapServers = "kafka:9093", SecurityProtocol = "SASL_SSL" };

        Validar(configuracao).Failures.Should().Equal(
            "Kafka:SaslMechanism é obrigatório com SecurityProtocol=SASL_SSL.",
            "Kafka:SslCaLocation ou Kafka:SslCaPem é obrigatório com SecurityProtocol=SASL_SSL (política Uni+: CA explícito).");
    }

    [Fact]
    public void Validate_CamposSaslESslEmPlaintext_DevolveCoerenciaDoSaslDepoisDoSsl()
    {
        KafkaSettings configuracao = new()
        {
            BootstrapServers = "kafka:9092",
            SaslMechanism = "PLAIN",
            SslCaPem = "conteudo-ficticio",
        };

        Validar(configuracao).Failures.Should().Equal(
            "Kafka:SaslMechanism/SaslUsername/SaslPassword exigem SecurityProtocol=SASL_PLAINTEXT ou SASL_SSL.",
            "Kafka:SslCaLocation/SslCaPem exigem SecurityProtocol=SSL ou SASL_SSL.");
    }

    [Fact]
    public void Validate_NomeDeOpcaoQualquer_ValidaIgualAoPadrao()
    {
        KafkaSettings configuracao = new() { BootstrapServers = "kafka:9092", SecurityProtocol = "MTLS" };

        Validar(configuracao, nome: "outra").Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_OpcoesNulas_LancaArgumentNull()
    {
        Action validar = () => Validar(null!);

        validar.Should().Throw<ArgumentNullException>();
    }
}
