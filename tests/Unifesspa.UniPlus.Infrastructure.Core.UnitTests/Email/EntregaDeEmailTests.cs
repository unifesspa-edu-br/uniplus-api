namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using MailKit.Net.Smtp;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Wolverine;

public sealed class EntregaDeEmailTests
{
    private const string Assunto = "Código de instalação 481516";
    private const string CorpoTexto = "Seu código de confirmação é 481516.";
    private const string CorpoHtml = "<p>Seu código de confirmação é <b>481516</b>.</p>";

    private readonly ITransporteDeEmail _transporte = Substitute.For<ITransporteDeEmail>();
    private readonly LoggerDeCaptura _logger = new();

    [Fact(DisplayName = "O log do envio traz a finalidade e o e-mail mascarado, sem assunto nem corpo")]
    public async Task Handle_Enviado_LogSemConteudoEComEmailMascarado()
    {
        EnviarEmail mensagem = NovaMensagem();

        await Entrega().Handle(mensagem, new Envelope { Attempts = 1 }, CancellationToken.None);

        await _transporte.Received(1).EnviarAsync(mensagem, Arg.Any<CancellationToken>());
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
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<CancellationToken>())
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
        await _transporte.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default);
    }

    [Fact(DisplayName = "Rejeição 5xx do relay vai para mortas, saneada e sem o endereço, sem passar pelo retry")]
    public async Task Handle_Rejeicao5xx_LancaRecusaPermanenteSaneada()
    {
        EnviarEmail mensagem = NovaMensagem();
        _transporte.EnviarAsync(Arg.Any<EnviarEmail>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new SmtpCommandException(
                SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 candidato@unifesspa.edu.br inexistente"));

        Func<Task> envio = () => Entrega().Handle(mensagem, new Envelope(), CancellationToken.None);

        EnvioDeEmailRecusadoException recusa = (await envio.Should().ThrowAsync<EnvioDeEmailRecusadoException>()).Which;
        recusa.InnerException.Should().BeNull();
        recusa.ToString().Should().NotContain("candidato@unifesspa.edu.br");
        _logger.Texto.Should().Contain("instalacao.codigo").And.Contain(mensagem.MensagemId.ToString())
            .And.NotContain("candidato@unifesspa.edu.br");
    }

    private EntregaDeEmail Entrega() => new(_transporte, _logger);

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, Assunto, CorpoTexto, CorpoHtml, "instalacao.codigo");

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
