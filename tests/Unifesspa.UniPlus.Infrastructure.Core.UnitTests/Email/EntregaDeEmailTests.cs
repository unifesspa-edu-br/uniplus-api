namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using MailKit.Net.Smtp;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Wolverine;

public sealed class EntregaDeEmailTests : IDisposable
{
    private const string Assunto = "Código de instalação 481516";
    private const string CorpoTexto = "Seu código de confirmação é 481516.";
    private const string CorpoHtml = "<p>Seu código de confirmação é <b>481516</b>.</p>";

    private readonly ITransporteDeEmail _transporte = Substitute.For<ITransporteDeEmail>();
    private readonly LoggerDeCaptura _logger = new();
    private readonly RelogioFixo _relogio = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly MetricasDaFilaDeEmail _metricas;
    private readonly ColetorDeMedidas _coletor;

    public EntregaDeEmailTests()
    {
        _metricas = new MetricasDaFilaDeEmail(_relogio);
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<Action>(), Arg.Any<CancellationToken>()).Returns(c =>
        {
            c.Arg<Action>()();
            return Task.CompletedTask;
        });
        _coletor = new ColetorDeMedidas(_metricas.Medidor);
    }

    public void Dispose()
    {
        _coletor.Dispose();
        _metricas.Dispose();
    }

    [Fact(DisplayName = "O log do envio traz a finalidade e o e-mail mascarado, sem assunto nem corpo")]
    public async Task Handle_Enviado_LogSemConteudoEComEmailMascarado()
    {
        EnviarEmail mensagem = NovaMensagem();

        await Entrega().Handle(mensagem, new Envelope { Attempts = 1 }, CancellationToken.None);

        await _transporte.Received(1).EnviarAsync(mensagem, Arg.Any<Action>(), Arg.Any<CancellationToken>());
        string log = _logger.Texto;
        log.Should().Contain("instalacao.codigo").And.Contain("c***@unifesspa.edu.br");
        log.Should().NotContain("candidato@unifesspa.edu.br")
            .And.NotContain("481516")
            .And.NotContain(Assunto)
            .And.NotContain(CorpoTexto);
    }

    [Fact(DisplayName = "A falha do transporte sobe saneada para o Wolverine reenviar, sem o endereço na exceção nem no log")]
    public async Task Handle_FalhaDoTransporte_RelancaESemEmailNoLog()
    {
        EnviarEmail mensagem = NovaMensagem();
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<Action>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("550 rejeitado para candidato@unifesspa.edu.br"));

        Func<Task> envio = () => Entrega().Handle(mensagem, new Envelope { Attempts = 3 }, CancellationToken.None);

        FalhaNoEnvioDeEmailException falha = (await envio.Should().ThrowAsync<FalhaNoEnvioDeEmailException>()).Which;
        falha.InnerException.Should().BeNull();
        falha.ToString().Should().NotContain("candidato@unifesspa.edu.br")
            .And.Contain(mensagem.MensagemId.ToString()).And.Contain(nameof(InvalidOperationException));
        string log = _logger.Texto;
        log.Should().Contain("tentativa 3").And.Contain("c***@unifesspa.edu.br");
        log.Should().NotContain("candidato@unifesspa.edu.br").And.NotContain("481516");
    }

    [Fact(DisplayName = "Destinatário que não é e-mail é logado e recusado sem tentar o transporte")]
    public async Task Handle_DestinatarioInvalido_NaoTentaOTransporte()
    {
        EnviarEmail invalida = NovaMensagem() with { Destinatario = "não é e-mail" };

        Func<Task> envio = () => Entrega().Handle(invalida, new Envelope(), CancellationToken.None);

        await envio.Should().ThrowAsync<DestinatarioInvalidoException>();
        _logger.Texto.Should().Contain("instalacao.codigo").And.Contain(invalida.MensagemId.ToString());
        await _transporte.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Rejeição 5xx do relay vai para mortas, saneada e sem o endereço, sem passar pelo retry")]
    public async Task Handle_Rejeicao5xx_LancaRecusaPermanenteSaneada()
    {
        EnviarEmail mensagem = NovaMensagem();
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<Action>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new SmtpCommandException(
                SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 candidato@unifesspa.edu.br inexistente"));

        Func<Task> envio = () => Entrega().Handle(mensagem, new Envelope(), CancellationToken.None);

        EnvioDeEmailRecusadoException recusa = (await envio.Should().ThrowAsync<EnvioDeEmailRecusadoException>()).Which;
        recusa.InnerException.Should().BeNull();
        recusa.ToString().Should().NotContain("candidato@unifesspa.edu.br");
        _logger.Texto.Should().Contain("instalacao.codigo").And.Contain(mensagem.MensagemId.ToString())
            .And.NotContain("candidato@unifesspa.edu.br");
    }

    [Fact(DisplayName = "O aceite do relay soma em enviados e guarda o instante do último envio")]
    public async Task Handle_Enviado_SomaEmEnviadosEGuardaOInstante()
    {
        await Entrega().Handle(NovaMensagem(), new Envelope(), CancellationToken.None);

        _coletor.Soma("uniplus.email.enviados").Should().Be(1);
        _coletor.Ultima("uniplus.email.ultimo_envio").Should().Be(_relogio.GetUtcNow().ToUnixTimeSeconds());
        _coletor.Ultima("uniplus.email.ultima_falha").Should().Be(0, "nenhuma falha aconteceu");
    }

    [Fact(DisplayName = "A falha transitória soma em falhas por tipo e guarda o instante da última falha")]
    public async Task Handle_FalhaTransitoria_SomaEmFalhasTransitoria()
    {
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<Action>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new InvalidOperationException());

        Func<Task> envio = () => Entrega().Handle(NovaMensagem(), new Envelope(), CancellationToken.None);

        await envio.Should().ThrowAsync<FalhaNoEnvioDeEmailException>();
        _coletor.Soma("uniplus.email.falhas", "tipo", "transitoria").Should().Be(1);
        _coletor.Soma("uniplus.email.falhas", "tipo", "permanente").Should().Be(0);
        _coletor.Ultima("uniplus.email.ultima_falha").Should().Be(_relogio.GetUtcNow().ToUnixTimeSeconds());
        _coletor.Ultima("uniplus.email.ultimo_envio").Should().Be(0, "nenhum aceite aconteceu");
    }

    [Fact(DisplayName = "A recusa permanente do relay soma em falhas como permanente")]
    public async Task Handle_Rejeicao5xx_SomaEmFalhasPermanente()
    {
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<Action>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550"));

        Func<Task> envio = () => Entrega().Handle(NovaMensagem(), new Envelope(), CancellationToken.None);

        await envio.Should().ThrowAsync<EnvioDeEmailRecusadoException>();
        _coletor.Soma("uniplus.email.falhas", "tipo", "permanente").Should().Be(1);
    }

    [Fact(DisplayName = "O atraso até o aceite entra no histograma em segundos, sem rótulo de destinatário, assunto ou finalidade")]
    public async Task Handle_Enviado_RegistraOAtrasoSemDadoPessoal()
    {
        Envelope envelope = new() { SentAt = _relogio.GetUtcNow().AddSeconds(-82) };

        await Entrega().Handle(NovaMensagem(), envelope, CancellationToken.None);

        MedidaColhida atraso = _coletor.Registradas("uniplus.email.atraso").Should().ContainSingle().Which;
        atraso.Valor.Should().Be(82);
        atraso.Etiquetas.Should().BeEmpty("o atraso não pode carregar destinatário, assunto nem finalidade");
    }

    [Fact(DisplayName = "Atraso negativo, de relógios divergentes, é registrado como zero")]
    public async Task Handle_PublicacaoNoFuturo_RegistraAtrasoZero()
    {
        Envelope envelope = new() { SentAt = _relogio.GetUtcNow().AddSeconds(3) };

        await Entrega().Handle(NovaMensagem(), envelope, CancellationToken.None);

        _coletor.Registradas("uniplus.email.atraso").Should().ContainSingle().Which.Valor.Should().Be(0);
    }

    // Prova só a classificação: o prazo vencido sai como falha transitória, a exceção que a política da chain reenvia.
    // A volta à fila com o Wolverine real é provada pelo teste do Host "Falha transitória do relay não perde a mensagem:
    // ela chega na nova tentativa" (EnvioDeEmailTests).
    [Fact(DisplayName = "Prazo vencido no relay é classificado como falha transitória, não como recusa permanente")]
    public async Task Handle_PrazoVencido_ClassificadoComoTransitorio()
    {
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<Action>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new TimeoutException());

        Func<Task> envio = () => Entrega().Handle(NovaMensagem(), new Envelope { Attempts = 1 }, CancellationToken.None);

        await envio.Should().ThrowAsync<FalhaNoEnvioDeEmailException>();
        _coletor.Soma("uniplus.email.falhas", "tipo", "transitoria").Should().Be(1);
        _coletor.Soma("uniplus.email.falhas", "tipo", "permanente").Should().Be(0, "prazo vencido não vai para as mortas");
        _coletor.Registradas("uniplus.email.atraso").Should().BeEmpty("nada foi aceito");
    }

    private EntregaDeEmail Entrega() => new(_transporte, _metricas, _relogio, _logger);

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, Assunto, CorpoTexto, CorpoHtml, "instalacao.codigo");

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class LoggerDeCaptura : ILogger<EntregaDeEmail>
    {
        private readonly List<string> _linhas = [];

        public string Texto => string.Join('\n', _linhas);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            // A exceção também é lida: o que ela carrega iria para o sink do Serilog.
            _linhas.Add(formatter(state, exception) + exception);
        }
    }
}
