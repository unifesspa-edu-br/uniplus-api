namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Messaging;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Messaging;

/// <summary>
/// Compara a lista completa e ordenada de mensagens da validação de segurança do Kafka: a ordem das mensagens
/// e a dependência entre as regras fazem parte do contrato.
/// </summary>
public sealed class KafkaSecurityValidationTests
{
    private const string UsuarioFicticio = "usuario-ficticio";
    private const string SenhaFicticia = "senha-ficticia";
    private const string CaCaminho = "/etc/uniplus-kafka/ca.crt";
    private const string CaConteudo = "conteudo-ficticio-da-ca";
    private const string SaslSsl = "SASL_SSL";
    private const string MecanismoInvalido = "Kafka:SaslMechanism 'DIGEST-MD5' inválido. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512.";
    private const string CamposSaslSemProtocoloSasl = "Kafka:SaslMechanism/SaslUsername/SaslPassword exigem SecurityProtocol=SASL_PLAINTEXT ou SASL_SSL.";
    private const string CamposSslSemProtocoloSsl = "Kafka:SslCaLocation/SslCaPem exigem SecurityProtocol=SSL ou SASL_SSL.";
    private const string CaAmbiguo = "Kafka:SslCaLocation e Kafka:SslCaPem são mutuamente exclusivos — preencha apenas um.";

    private static readonly KafkaSecurityFields Fields = new(
        "Kafka",
        "SecurityProtocol",
        "SaslMechanism",
        "SaslUsername",
        "SaslPassword",
        "SslCaLocation",
        "SslCaPem");

    private static string Obrigatorio(string campo, string protocolo) =>
        $"Kafka:{campo} é obrigatório com SecurityProtocol={protocolo}.";

    private static string CaObrigatorio(string protocolo) =>
        $"Kafka:SslCaLocation ou Kafka:SslCaPem é obrigatório com SecurityProtocol={protocolo} (política Uni+: CA explícito).";

    private static List<string> Validar(
        string? protocolo,
        string? mecanismo = null,
        string? usuario = null,
        string? senha = null,
        string? caLocation = null,
        string? caPem = null) =>
        KafkaSecurityValidation.Validar(Fields, protocolo, mecanismo, usuario, senha, caLocation, caPem);

    [Theory]
    [InlineData("MTLS")]
    [InlineData("1")]
    [InlineData("-1")]
    public void Validar_ProtocoloInvalidoComSaslECaPreenchidos_DevolveSoAMensagemDoProtocolo(string protocolo)
    {
        List<string> falhas = Validar(protocolo, "PLAIN", UsuarioFicticio, SenhaFicticia, CaCaminho, CaConteudo);

        falhas.Should().Equal($"Kafka:SecurityProtocol '{protocolo}' inválido. Use PLAINTEXT, SSL, SASL_PLAINTEXT ou SASL_SSL.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_ProtocoloAusenteSemOutrosCampos_DevolveListaVazia(string? protocolo)
    {
        Validar(protocolo).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_MecanismoAusenteSemCredenciais_DevolveSoMecanismoObrigatorio(string? mecanismo)
    {
        List<string> falhas = Validar(SaslSsl, mecanismo, caLocation: CaCaminho);

        falhas.Should().Equal(Obrigatorio("SaslMechanism", SaslSsl));
    }

    [Fact]
    public void Validar_MecanismoInvalidoSemCredenciais_DevolveSoMecanismoInvalido()
    {
        List<string> falhas = Validar(SaslSsl, "DIGEST-MD5", caLocation: CaCaminho);

        falhas.Should().Equal(MecanismoInvalido);
    }

    [Theory]
    [InlineData("OAUTHBEARER")]
    [InlineData("GSSAPI")]
    public void Validar_MecanismoNaoSuportadoSemCredenciais_DevolveSoMecanismoNaoSuportado(string mecanismo)
    {
        List<string> falhas = Validar(SaslSsl, mecanismo, caLocation: CaCaminho);

        falhas.Should().Equal(
            $"Kafka:SaslMechanism '{mecanismo}' não é suportado pelo Uni+. Use PLAIN, SCRAM-SHA-256 ou SCRAM-SHA-512.");
    }

    [Theory]
    [InlineData("PLAIN")]
    [InlineData("SCRAM-SHA-256")]
    [InlineData("SCRAM-SHA-512")]
    public void Validar_MecanismoValidoSemUsuarioENemSenha_DevolveUsuarioDepoisSenha(string mecanismo)
    {
        List<string> falhas = Validar(SaslSsl, mecanismo, caLocation: CaCaminho);

        falhas.Should().Equal(Obrigatorio("SaslUsername", SaslSsl), Obrigatorio("SaslPassword", SaslSsl));
    }

    [Fact]
    public void Validar_MecanismoValidoSemUsuario_DevolveSoUsuarioObrigatorio()
    {
        List<string> falhas = Validar(SaslSsl, "SCRAM-SHA-512", senha: SenhaFicticia, caLocation: CaCaminho);

        falhas.Should().Equal(Obrigatorio("SaslUsername", SaslSsl));
    }

    [Fact]
    public void Validar_MecanismoValidoSemSenha_DevolveSoSenhaObrigatoria()
    {
        List<string> falhas = Validar(SaslSsl, "SCRAM-SHA-512", usuario: UsuarioFicticio, caLocation: CaCaminho);

        falhas.Should().Equal(Obrigatorio("SaslPassword", SaslSsl));
    }

    [Fact]
    public void Validar_CaObrigatorioComCredenciaisAusentes_FicaDepoisDasFalhasDoSasl()
    {
        List<string> falhas = Validar(SaslSsl, "PLAIN");

        falhas.Should().Equal(
            Obrigatorio("SaslUsername", SaslSsl),
            Obrigatorio("SaslPassword", SaslSsl),
            CaObrigatorio(SaslSsl));
    }

    [Fact]
    public void Validar_CaObrigatorioComMecanismoAusente_FicaDepoisDoMecanismoObrigatorio()
    {
        List<string> falhas = Validar(SaslSsl);

        falhas.Should().Equal(Obrigatorio("SaslMechanism", SaslSsl), CaObrigatorio(SaslSsl));
    }

    [Fact]
    public void Validar_ProtocoloSslSemCa_DevolveSoCaObrigatorio()
    {
        Validar("SSL").Should().Equal(CaObrigatorio("SSL"));
    }

    [Fact]
    public void Validar_CaAmbiguoComCamposSaslEmProtocoloSemSasl_FicaDepoisDasDuasCoerencias()
    {
        List<string> falhas = Validar("PLAINTEXT", "PLAIN", UsuarioFicticio, SenhaFicticia, CaCaminho, CaConteudo);

        falhas.Should().Equal(CamposSaslSemProtocoloSasl, CamposSslSemProtocoloSsl, CaAmbiguo);
    }

    [Fact]
    public void Validar_CaAmbiguoComMecanismoAusente_FicaAntesDasFalhasDoSasl()
    {
        List<string> falhas = Validar(SaslSsl, caLocation: CaCaminho, caPem: CaConteudo);

        falhas.Should().Equal(CaAmbiguo, Obrigatorio("SaslMechanism", SaslSsl));
    }

    [Fact]
    public void Validar_CaAmbiguoEmProtocoloSsl_DevolveSoAAmbiguidade()
    {
        Validar("SSL", caLocation: CaCaminho, caPem: CaConteudo).Should().Equal(CaAmbiguo);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("SSL", CaCaminho)]
    public void Validar_CamposSaslSemProtocoloSasl_DevolveSoACoerenciaDoSasl(string? protocolo, string? caLocation)
    {
        List<string> falhas = Validar(protocolo, "PLAIN", UsuarioFicticio, SenhaFicticia, caLocation);

        falhas.Should().Equal(CamposSaslSemProtocoloSasl);
    }

    [Fact]
    public void Validar_CaSemProtocoloSsl_DevolveSoACoerenciaDoSsl()
    {
        Validar(protocolo: null, caLocation: CaCaminho).Should().Equal(CamposSslSemProtocoloSsl);
    }

    [Fact]
    public void Validar_CaComSaslPlaintextCompleto_DevolveSoACoerenciaDoSsl()
    {
        List<string> falhas = Validar("SASL_PLAINTEXT", "PLAIN", UsuarioFicticio, SenhaFicticia, CaCaminho);

        falhas.Should().Equal(CamposSslSemProtocoloSsl);
    }

    [Theory]
    [InlineData("SASL_SSL", null, CaConteudo)]
    [InlineData("sasl_ssl", CaCaminho, null)]
    public void Validar_SaslSslCompletoComUmaCa_DevolveListaVazia(string protocolo, string? caLocation, string? caPem)
    {
        List<string> falhas = Validar(protocolo, "PLAIN", UsuarioFicticio, SenhaFicticia, caLocation, caPem);

        falhas.Should().BeEmpty();
    }

    [Fact]
    public void Validar_ProtocoloEmMinusculasSemMecanismo_RepeteOTextoInformadoNasMensagens()
    {
        List<string> falhas = Validar("sasl_ssl", caLocation: CaCaminho);

        falhas.Should().Equal(Obrigatorio("SaslMechanism", "sasl_ssl"));
    }

    [Fact]
    public void Validar_MecanismoNaoSuportadoComCredenciais_NaoRepeteUsuarioNemSenhaNasMensagens()
    {
        List<string> falhas = Validar(SaslSsl, "OAUTHBEARER", UsuarioFicticio, SenhaFicticia, CaCaminho, CaConteudo);

        falhas.Should().OnlyContain(
            mensagem => !mensagem.Contains(UsuarioFicticio, StringComparison.Ordinal)
                && !mensagem.Contains(SenhaFicticia, StringComparison.Ordinal));
    }
}
