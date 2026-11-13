namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using MailKit.Net.Smtp;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Wolverine;

/// <summary>A entrega com o transporte SMTP de verdade sobre um cliente dublê: o atraso termina no aceite do relay.</summary>
public sealed class AtrasoAteOAceiteTests : IDisposable
{
    private static readonly DateTimeOffset Publicacao = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly RelogioManual _relogio = new(Publicacao);
    private readonly MetricasDaFilaDeEmail _metricas;
    private readonly ColetorDeMedidas _coletor;

    public AtrasoAteOAceiteTests()
    {
        _metricas = new MetricasDaFilaDeEmail(_relogio);
        _coletor = new ColetorDeMedidas(_metricas.Medidor);
    }

    public void Dispose()
    {
        _coletor.Dispose();
        _metricas.Dispose();
    }

    [Fact(DisplayName = "Relay que aceita rápido e demora no QUIT não infla o atraso até o aceite")]
    public async Task Handle_EncerramentoLento_AtrasoParaNoAceite()
    {
        ISmtpClient cliente = Substitute.For<ISmtpClient>();
        cliente.SendAsync(Arg.Any<MimeKit.MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<MailKit.ITransferProgress>())
            .Returns(_ => Avancar(TimeSpan.FromSeconds(2), "250 aceita"));
        cliente.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Avancar(TimeSpan.FromSeconds(8), string.Empty));

        await Entrega(cliente).Handle(NovaMensagem(), new Envelope { SentAt = Publicacao }, CancellationToken.None);

        _coletor.Registradas("uniplus.email.atraso").Should().ContainSingle().Which.Valor.Should().Be(2);
    }

    private Task<string> Avancar(TimeSpan quanto, string resposta)
    {
        _relogio.Avancar(quanto);
        return Task.FromResult(resposta);
    }

    private EntregaDeEmail Entrega(ISmtpClient cliente)
    {
        SmtpTransporteDeEmail transporte = new(
            Options.Create(new EmailSmtpOptions
            {
                Host = "relay.unifesspa.edu.br",
                Porta = 587,
                Remetente = "nao-responda@unifesspa.edu.br",
            }),
            NullLogger<SmtpTransporteDeEmail>.Instance,
            () => cliente);

        return new EntregaDeEmail(transporte, _metricas, _relogio, NullLogger<EntregaDeEmail>.Instance);
    }

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, "Aviso de pedido", "Há um pedido aguardando sua análise.",
        "<p>Há um pedido aguardando sua análise.</p>", "governanca.pedido");

    private sealed class RelogioManual(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }
}
