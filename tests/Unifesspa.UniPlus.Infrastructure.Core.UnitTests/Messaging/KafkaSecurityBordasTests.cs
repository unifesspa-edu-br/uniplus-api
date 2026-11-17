namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Confluent.Kafka;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

public sealed class KafkaSecurityBordasTests
{
    [Theory]
    [InlineData(" SASL_SSL ", SecurityProtocol.SaslSsl)]
    [InlineData("saslplaintext", SecurityProtocol.SaslPlaintext)]
    public void TryParseSecurityProtocol_ComEspacosOuSemSublinhado_Aceita(string bruto, SecurityProtocol esperado)
    {
        bool aceito = KafkaSecurity.TryParseSecurityProtocol(bruto, out SecurityProtocol protocolo);

        aceito.Should().BeTrue();
        protocolo.Should().Be(esperado);
    }

    [Theory]
    [InlineData("OAUTHBEARER", SaslMechanism.OAuthBearer)]
    [InlineData("GSSAPI", SaslMechanism.Gssapi)]
    [InlineData("scram-sha-256", SaslMechanism.ScramSha256)]
    [InlineData("PLAIN", SaslMechanism.Plain)]
    public void TryParseSaslMechanism_NomesDoEnum_Aceita(string bruto, SaslMechanism esperado)
    {
        bool aceito = KafkaSecurity.TryParseSaslMechanism(bruto, out SaslMechanism mecanismo);

        aceito.Should().BeTrue();
        mecanismo.Should().Be(esperado);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("   ")]
    public void TryParseSecurityProtocol_Numero_Rejeita(string bruto)
    {
        KafkaSecurity.TryParseSecurityProtocol(bruto, out _).Should().BeFalse();
    }

    [Fact]
    public void Apply_ProtocoloEMecanismoInvalidos_NaoAlteramOClientConfig()
    {
        KafkaSettings configuracao = new() { SecurityProtocol = "MTLS", SaslMechanism = "DIGEST-MD5" };
        ClientConfig cliente = new();

        KafkaSecurity.Apply(cliente, configuracao);

        cliente.SecurityProtocol.Should().BeNull();
        cliente.SaslMechanism.Should().BeNull();
    }

    [Fact]
    public void Apply_CredenciaisECaEmBranco_NaoAlteramOClientConfig()
    {
        KafkaSettings configuracao = new()
        {
            SaslUsername = "  ",
            SaslPassword = "",
            SslCaLocation = " ",
            SslCaPem = "  ",
        };
        ClientConfig cliente = new();

        KafkaSecurity.Apply(cliente, configuracao);

        cliente.Should().BeEquivalentTo(new ClientConfig());
    }

    [Fact]
    public void Apply_CaLocationECaPem_PreencheOsDois()
    {
        KafkaSettings configuracao = new() { SslCaLocation = "/etc/ca.crt", SslCaPem = "conteudo-ficticio" };
        ClientConfig cliente = new();

        KafkaSecurity.Apply(cliente, configuracao);

        cliente.SslCaLocation.Should().Be("/etc/ca.crt");
        cliente.SslCaPem.Should().Be("conteudo-ficticio");
    }

    [Fact]
    public void Apply_ClientConfigNulo_LancaArgumentNull()
    {
        Action aplicar = () => KafkaSecurity.Apply(null!, new KafkaSettings());

        aplicar.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ConfiguracaoNula_LancaArgumentNull()
    {
        Action aplicar = () => KafkaSecurity.Apply(new ClientConfig(), null!);

        aplicar.Should().Throw<ArgumentNullException>();
    }
}
