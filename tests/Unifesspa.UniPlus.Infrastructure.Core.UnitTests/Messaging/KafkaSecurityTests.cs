namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Confluent.Kafka;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public sealed class KafkaSecurityTests
{
    [Theory]
    [InlineData("PLAINTEXT", SecurityProtocol.Plaintext)]
    [InlineData("Plaintext", SecurityProtocol.Plaintext)]
    [InlineData("SSL", SecurityProtocol.Ssl)]
    [InlineData("SASL_SSL", SecurityProtocol.SaslSsl)]
    [InlineData("SaslSsl", SecurityProtocol.SaslSsl)]
    [InlineData("sasl_ssl", SecurityProtocol.SaslSsl)]
    [InlineData("SASL_PLAINTEXT", SecurityProtocol.SaslPlaintext)]
    public void TryParseSecurityProtocol_AceitaFormasConvencionais(string raw, SecurityProtocol expected)
    {
        bool ok = KafkaSecurity.TryParseSecurityProtocol(raw, out SecurityProtocol parsed);
        ok.Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MTLS")]
    [InlineData("SASL")]
    // Strings numéricas: Enum.TryParse aceita por default, mas IsDefined garante que só
    // nomes legítimos do enum passem. Sem isso, "999" parseia para SecurityProtocol)999 e
    // vaza para ClientConfig, gerando falha tardia em runtime.
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("100")]
    public void TryParseSecurityProtocol_RejeitaInvalido(string? raw)
    {
        bool ok = KafkaSecurity.TryParseSecurityProtocol(raw, out _);
        ok.Should().BeFalse();
    }

    [Theory]
    [InlineData("PLAIN", SaslMechanism.Plain)]
    [InlineData("Plain", SaslMechanism.Plain)]
    [InlineData("SCRAM-SHA-256", SaslMechanism.ScramSha256)]
    [InlineData("ScramSha256", SaslMechanism.ScramSha256)]
    [InlineData("SCRAM-SHA-512", SaslMechanism.ScramSha512)]
    [InlineData("ScramSha512", SaslMechanism.ScramSha512)]
    [InlineData("scram-sha-512", SaslMechanism.ScramSha512)]
    [InlineData("OAUTHBEARER", SaslMechanism.OAuthBearer)]
    [InlineData("GSSAPI", SaslMechanism.Gssapi)]
    public void TryParseSaslMechanism_AceitaFormasConvencionais(string raw, SaslMechanism expected)
    {
        bool ok = KafkaSecurity.TryParseSaslMechanism(raw, out SaslMechanism parsed);
        ok.Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("DIGEST-MD5")]
    // Strings numéricas fora do range nominal são rejeitadas pelo IsDefined (mesmo motivo
    // documentado em TryParseSecurityProtocol_RejeitaInvalido).
    [InlineData("999")]
    [InlineData("-1")]
    [InlineData("42")]
    public void TryParseSaslMechanism_RejeitaInvalido(string? raw)
    {
        bool ok = KafkaSecurity.TryParseSaslMechanism(raw, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void Apply_SaslSslComScramSha512_PreencheTodosOsCamposNoClientConfig()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SASL_SSL",
            SaslMechanism = "SCRAM-SHA-512",
            SaslUsername = "uniplus",
            SaslPassword = "secret",
            SslCaLocation = "/etc/uniplus-kafka/ca.crt",
        };

        ClientConfig config = new();
        KafkaSecurity.Apply(config, settings);

        config.SecurityProtocol.Should().Be(SecurityProtocol.SaslSsl);
        config.SaslMechanism.Should().Be(SaslMechanism.ScramSha512);
        config.SaslUsername.Should().Be("uniplus");
        config.SaslPassword.Should().Be("secret");
        config.SslCaLocation.Should().Be("/etc/uniplus-kafka/ca.crt");
    }

    [Fact]
    public void Apply_SoBootstrap_NaoMexeEmCamposVazios()
    {
        // Em PLAINTEXT (sem nenhum override), Apply é no-op exceto BootstrapServers que
        // é gerenciado fora. Verifica que campos de SASL/SSL ficam null.
        KafkaSettings settings = new() { BootstrapServers = "kafka:9092" };

        ClientConfig config = new();
        KafkaSecurity.Apply(config, settings);

        config.SecurityProtocol.Should().BeNull();
        config.SaslMechanism.Should().BeNull();
        config.SaslUsername.Should().BeNullOrEmpty();
        config.SaslPassword.Should().BeNullOrEmpty();
    }

    [Fact]
    public void Apply_SslCaPem_PreencheCampoCorrespondente()
    {
        KafkaSettings settings = new()
        {
            BootstrapServers = "kafka:9092",
            SecurityProtocol = "SSL",
            SslCaPem = "-----BEGIN CERTIFICATE-----\nMIIB...",
        };

        ClientConfig config = new();
        KafkaSecurity.Apply(config, settings);

        config.SslCaPem.Should().StartWith("-----BEGIN CERTIFICATE-----");
    }
}
