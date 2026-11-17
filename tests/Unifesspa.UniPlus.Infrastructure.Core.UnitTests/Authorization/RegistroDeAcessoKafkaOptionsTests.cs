namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Confluent.Kafka;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

public sealed class RegistroDeAcessoKafkaOptionsTests
{
    private const string Secao = "RegistroDeAcesso:Kafka";
    private const string BootstrapObrigatorio =
        Secao + ":BootstrapServers é obrigatório: o registro de acesso a dado pessoal é publicado no Kafka, e sem ele a aplicação não entrega dado protegido.";
    private const string TopicoVazio = Secao + ":Topico não pode ser vazio.";
    private const string PrazoForaDaFaixa = Secao + ":PrazoDeConfirmacao deve estar entre 100 ms e 2147483647 ms.";

    private static RegistroDeAcessoKafkaOptions Validas() => new() { BootstrapServers = "kafka:9092" };

    private static RegistroDeAcessoKafkaOptions SaslSslCompleto() => new()
    {
        BootstrapServers = "kafka:9093",
        SecurityProtocol = "SASL_SSL",
        SaslMechanism = "SCRAM-SHA-512",
        Usuario = "usuario-ficticio",
        Senha = "senha-ficticia",
        CaminhoDoCertificadoCa = "/etc/ca.crt",
    };

    private static ValidateOptionsResult Validar(RegistroDeAcessoKafkaOptions opcoes) =>
        new RegistroDeAcessoKafkaOptionsValidator().Validate(Options.DefaultName, opcoes);

    [Fact]
    public void Validate_OpcoesPadraoComBootstrap_DevolveSucesso()
    {
        Validar(Validas()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_TudoInvalido_DevolveBootstrapTopicoPrazoESegurancaNessaOrdem()
    {
        RegistroDeAcessoKafkaOptions opcoes = new()
        {
            BootstrapServers = " ",
            Topico = "",
            PrazoDeConfirmacao = TimeSpan.FromMilliseconds(99),
            SecurityProtocol = "SSL",
        };

        Validar(opcoes).Failures.Should().Equal(
            BootstrapObrigatorio,
            TopicoVazio,
            PrazoForaDaFaixa,
            Secao + ":CaminhoDoCertificadoCa é obrigatório com SecurityProtocol=SSL (política Uni+: CA explícito).");
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(99, false)]
    public void Validate_PrazoNoLimiteInferior_RecusaSoAbaixoDoMinimo(int milissegundos, bool aceito)
    {
        RegistroDeAcessoKafkaOptions opcoes = Validas();
        opcoes.PrazoDeConfirmacao = TimeSpan.FromMilliseconds(milissegundos);

        Validar(opcoes).Succeeded.Should().Be(aceito);
    }

    [Fact]
    public void Validate_PrazoAcimaDoMaximoEmMilissegundosInteiros_Recusa()
    {
        RegistroDeAcessoKafkaOptions opcoes = Validas();
        opcoes.PrazoDeConfirmacao = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);

        Validar(opcoes).Failures.Should().Equal(PrazoForaDaFaixa);
    }

    [Fact]
    public void Validate_CaminhoDaCaSemProtocoloSsl_DevolveCoerenciaSemCaPem()
    {
        RegistroDeAcessoKafkaOptions opcoes = Validas();
        opcoes.CaminhoDoCertificadoCa = "/etc/ca.crt";

        Validar(opcoes).Failures.Should().Equal(
            Secao + ":CaminhoDoCertificadoCa exigem SecurityProtocol=SSL ou SASL_SSL.");
    }

    [Fact]
    public void Validate_SaslSslSemMecanismoENemCa_DevolveMecanismoDepoisCa()
    {
        RegistroDeAcessoKafkaOptions opcoes = Validas();
        opcoes.SecurityProtocol = "SASL_SSL";

        Validar(opcoes).Failures.Should().Equal(
            Secao + ":SaslMechanism é obrigatório com SecurityProtocol=SASL_SSL.",
            Secao + ":CaminhoDoCertificadoCa é obrigatório com SecurityProtocol=SASL_SSL (política Uni+: CA explícito).");
    }

    [Fact]
    public void Validate_CredenciaisEmPlaintext_DevolveCoerenciaComOsNomesDoRegistro()
    {
        RegistroDeAcessoKafkaOptions opcoes = Validas();
        opcoes.Usuario = "usuario-ficticio";

        Validar(opcoes).Failures.Should().Equal(
            Secao + ":SaslMechanism/Usuario/Senha exigem SecurityProtocol=SASL_PLAINTEXT ou SASL_SSL.");
    }

    [Fact]
    public void Validate_OpcoesNulas_LancaArgumentNull()
    {
        Action validar = () => Validar(null!);

        validar.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParaProdutor_SaslSslCompleto_PreencheASeguranca()
    {
        ProducerConfig produtor = SaslSslCompleto().ParaProdutor();

        produtor.BootstrapServers.Should().Be("kafka:9093");
        produtor.SecurityProtocol.Should().Be(SecurityProtocol.SaslSsl);
        produtor.SaslMechanism.Should().Be(SaslMechanism.ScramSha512);
        produtor.SaslUsername.Should().Be("usuario-ficticio");
        produtor.SaslPassword.Should().Be("senha-ficticia");
        produtor.SslCaLocation.Should().Be("/etc/ca.crt");
    }

    [Fact]
    public void ParaProdutor_PrazoInformado_ViraTempoLimiteDaMensagemEmMilissegundos()
    {
        RegistroDeAcessoKafkaOptions opcoes = SaslSslCompleto();
        opcoes.PrazoDeConfirmacao = TimeSpan.FromSeconds(3);

        opcoes.ParaProdutor().MessageTimeoutMs.Should().Be(3_000);
    }

    [Fact]
    public void ParaProdutor_Padrao_ConfiguraEntregaConfirmadaEIdempotente()
    {
        ProducerConfig produtor = Validas().ParaProdutor();

        produtor.Acks.Should().Be(Acks.All);
        produtor.AllowAutoCreateTopics.Should().BeFalse();
        produtor.EnableIdempotence.Should().BeTrue();
        produtor.MessageTimeoutMs.Should().Be(10_000);
        produtor.ClientId.Should().Be("uniplus-registro-de-acesso");
        produtor.SecurityProtocol.Should().BeNull();
    }
}
