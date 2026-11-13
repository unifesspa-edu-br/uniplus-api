namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Email;

using AwesomeAssertions;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MimeKit;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class SmtpTransporteDeEmailTests
{
    private static readonly TimeSpan Prazo = TimeSpan.FromMilliseconds(200);

    private readonly Queue<ISmtpClient> _clientes = new();

    [Fact(DisplayName = "Relay que não completa a conexão estoura o prazo de conexão em vez de esperar indefinidamente")]
    public async Task EnviarAsync_ConexaoLenta_LancaTimeout()
    {
        ISmtpClient lento = ClienteQueNaoConecta();

        Func<Task> envio = () => Transporte().EnviarAsync(NovaMensagem(), CancellationToken.None);

        (await envio.Should().ThrowAsync<TimeoutException>()).Which.InnerException
            .Should().BeAssignableTo<OperationCanceledException>();
        await lento.DidNotReceiveWithAnyArgs().SendAsync(default!, default, default);
    }

    [Fact(DisplayName = "Relay que não confirma a entrega estoura o prazo de envio")]
    public async Task EnviarAsync_EnvioLento_LancaTimeout()
    {
        ISmtpClient cliente = NovoCliente();
        cliente.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(c => EsperarCancelamento<string>(c.ArgAt<CancellationToken>(1)));

        Func<Task> envio = () => Transporte().EnviarAsync(NovaMensagem(), CancellationToken.None);

        await envio.Should().ThrowAsync<TimeoutException>();
    }

    [Fact(DisplayName = "Desligamento do host durante a conexão continua cancelamento, não vira prazo vencido")]
    public async Task EnviarAsync_CancelamentoDeQuemChama_PropagaCancelamento()
    {
        ClienteQueNaoConecta();
        using CancellationTokenSource desligamento = new(TimeSpan.FromMilliseconds(50));
        EmailSmtpOptions opcoes = Opcoes();
        opcoes.TempoLimiteDeConexao = TimeSpan.FromMinutes(1);

        Func<Task> envio = () => Transporte(opcoes).EnviarAsync(NovaMensagem(), desligamento.Token);

        await envio.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "Uma conexão lenta não segura a mensagem seguinte, que sai enquanto a primeira ainda espera")]
    public async Task EnviarAsync_ConexaoLenta_MensagemSeguinteSai()
    {
        EmailSmtpOptions opcoes = Opcoes();
        opcoes.TempoLimiteDeConexao = TimeSpan.FromSeconds(1);
        ClienteQueNaoConecta();
        ISmtpClient rapido = NovoCliente();
        SmtpTransporteDeEmail transporte = Transporte(opcoes);

        Task primeira = transporte.EnviarAsync(NovaMensagem(), CancellationToken.None);
        await transporte.EnviarAsync(NovaMensagem(), CancellationToken.None);

        primeira.IsCompleted.Should().BeFalse("a primeira conexão ainda está dentro do prazo");
        await rapido.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
        await primeira.Awaiting(t => t).Should().ThrowAsync<TimeoutException>();
    }

    [Fact(DisplayName = "Depois de um prazo vencido, a nova tentativa abre outra conexão e entrega")]
    public async Task EnviarAsync_NovaTentativaDepoisDoPrazo_Entrega()
    {
        ClienteQueNaoConecta();
        ISmtpClient segundo = NovoCliente();
        SmtpTransporteDeEmail transporte = Transporte();
        EnviarEmail mensagem = NovaMensagem();

        await transporte.Awaiting(t => t.EnviarAsync(mensagem, CancellationToken.None)).Should().ThrowAsync<TimeoutException>();
        await transporte.EnviarAsync(mensagem, CancellationToken.None);

        await segundo.Received(1).SendAsync(
            Arg.Is<MimeMessage>(m => m.MessageId == mensagem.MensagemId.ToString("N") + "@unifesspa.edu.br"),
            Arg.Any<CancellationToken>(),
            Arg.Any<ITransferProgress>());
    }

    [Fact(DisplayName = "O prazo de envio vale para cada leitura e escrita na conexão")]
    public async Task EnviarAsync_ConfiguraOTempoLimiteDoCliente()
    {
        ISmtpClient cliente = NovoCliente();

        await Transporte().EnviarAsync(NovaMensagem(), CancellationToken.None);

        cliente.Timeout.Should().Be((int)Prazo.TotalMilliseconds);
        await cliente.Received(1).ConnectAsync("relay.unifesspa.edu.br", 587, SecureSocketOptions.StartTls, Arg.Any<CancellationToken>())
            ;
    }

    [Fact(DisplayName = "Encerramento que estoura o prazo depois do aceite não vira falha de envio")]
    public async Task EnviarAsync_EncerramentoLento_NaoFalha()
    {
        ISmtpClient cliente = NovoCliente();
        cliente.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(c => EsperarCancelamento(c.ArgAt<CancellationToken>(1)));

        await Transporte().EnviarAsync(NovaMensagem(), CancellationToken.None);

        await cliente.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact(DisplayName = "Com usuário configurado, autentica antes de enviar")]
    public async Task EnviarAsync_ComUsuario_Autentica()
    {
        ISmtpClient cliente = NovoCliente();
        EmailSmtpOptions opcoes = Opcoes();
        opcoes.Usuario = "relay";
        opcoes.Senha = "segredo";

        await Transporte(opcoes).EnviarAsync(NovaMensagem(), CancellationToken.None);

        await cliente.Received(1).AuthenticateAsync("relay", "segredo", Arg.Any<CancellationToken>());
    }

    private ISmtpClient NovoCliente()
    {
        ISmtpClient cliente = Substitute.For<ISmtpClient>();
        _clientes.Enqueue(cliente);
        return cliente;
    }

    private ISmtpClient ClienteQueNaoConecta()
    {
        ISmtpClient cliente = NovoCliente();
        cliente.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(c => EsperarCancelamento(c.ArgAt<CancellationToken>(3)));
        return cliente;
    }

    private static async Task EsperarCancelamento(CancellationToken cancellationToken) =>
        await Task.Delay(Timeout.Infinite, cancellationToken);

    private static async Task<T> EsperarCancelamento<T>(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return default!;
    }

    private SmtpTransporteDeEmail Transporte(EmailSmtpOptions? opcoes = null) =>
        new(Options.Create(opcoes ?? Opcoes()), NullLogger<SmtpTransporteDeEmail>.Instance, _clientes.Dequeue);

    private static EmailSmtpOptions Opcoes() => new()
    {
        Host = "relay.unifesspa.edu.br",
        Porta = 587,
        Seguranca = SegurancaSmtp.StartTls,
        Remetente = "nao-responda@unifesspa.edu.br",
        TempoLimiteDeConexao = Prazo,
        TempoLimiteDeEnvio = Prazo,
    };

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, "Aviso de pedido", "Há um pedido aguardando sua análise.",
        "<p>Há um pedido aguardando sua análise.</p>", "governanca.pedido");
}
