namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using System.Globalization;
using System.Net.Sockets;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;

using Unifesspa.UniPlus.Application.Abstractions.Email;

/// <summary>
/// Entrega cada mensagem numa conexão própria com o relay, com prazo na conexão e no envio: relay lento vira
/// <see cref="TimeoutException"/>, que a entrega trata como falha transitória e o Wolverine tenta de novo.
/// </summary>
/// <remarks>
/// Uma conexão por mensagem: o volume é baixo e evita reaproveitar sessão que o relay já derrubou. O que impede uma
/// conexão lenta de prender a fila é o prazo, não o reuso.
/// </remarks>
public sealed partial class SmtpTransporteDeEmail : ITransporteDeEmail
{
    private readonly EmailSmtpOptions _opcoes;
    private readonly ILogger<SmtpTransporteDeEmail> _logger;
    private readonly MailboxAddress _remetente;
    private readonly Func<ISmtpClient> _novoCliente;

    public SmtpTransporteDeEmail(IOptions<EmailSmtpOptions> options, ILogger<SmtpTransporteDeEmail> logger)
        : this(options, logger, static () => new SmtpClient())
    {
    }

    internal SmtpTransporteDeEmail(IOptions<EmailSmtpOptions> options, ILogger<SmtpTransporteDeEmail> logger, Func<ISmtpClient> novoCliente)
    {
        ArgumentNullException.ThrowIfNull(options);
        _opcoes = options.Value;
        _logger = logger;
        _novoCliente = novoCliente;
        _remetente = new MailboxAddress(_opcoes.NomeRemetente, _opcoes.Remetente);
    }

    public async Task EnviarAsync(EnviarEmail mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        using MimeMessage mime = Montar(mensagem);
        using ISmtpClient cliente = _novoCliente();
        cliente.Timeout = (int)_opcoes.TempoLimiteDeEnvio.TotalMilliseconds;

        await ComPrazoAsync(prazo => ConectarAsync(cliente, prazo), _opcoes.TempoLimiteDeConexao, cancellationToken).ConfigureAwait(false);
        await ComPrazoAsync(prazo => EntregarAsync(cliente, mime, prazo), _opcoes.TempoLimiteDeEnvio, cancellationToken).ConfigureAwait(false);
        await EncerrarAsync(cliente, cancellationToken).ConfigureAwait(false);
    }

    // O prazo vencido vira TimeoutException; o cancelamento de quem chamou (desligamento do host) continua cancelamento.
    private static async Task ComPrazoAsync(Func<CancellationToken, Task> etapa, TimeSpan limite, CancellationToken cancellationToken)
    {
        using CancellationTokenSource prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(limite);
        try
        {
            await etapa(prazo.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                string.Create(CultureInfo.InvariantCulture, $"O relay SMTP não respondeu em {limite.TotalSeconds:0.###} s."), ex);
        }
    }

    private Task ConectarAsync(ISmtpClient cliente, CancellationToken cancellationToken) =>
        cliente.ConnectAsync(_opcoes.Host, _opcoes.Porta, Seguranca(_opcoes.Seguranca), cancellationToken);

    private async Task EntregarAsync(ISmtpClient cliente, MimeMessage mime, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_opcoes.Usuario))
        {
            await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await cliente.SendAsync(mime, cancellationToken).ConfigureAwait(false);
    }

    // O relay já aceitou a mensagem: falhar o QUIT não pode virar falha de envio, senão o Wolverine reenvia e o
    // destinatário recebe duas vezes.
    private async Task EncerrarAsync(ISmtpClient cliente, CancellationToken cancellationToken)
    {
        try
        {
            await ComPrazoAsync(prazo => cliente.DisconnectAsync(quit: true, prazo), _opcoes.TempoLimiteDeConexao, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ProtocolException or CommandException or IOException or SocketException
            or TimeoutException or OperationCanceledException or ServiceNotConnectedException or ObjectDisposedException)
        {
            string tipoDoErro = ex.GetType().Name;
            LogFalhaAoDesconectar(_logger, tipoDoErro);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Relay SMTP falhou ao encerrar a sessão após a entrega: {TipoDoErro}")]
    private static partial void LogFalhaAoDesconectar(ILogger logger, string tipoDoErro);

    private MimeMessage Montar(EnviarEmail mensagem)
    {
        MimeMessage mime = new();
        mime.From.Add(_remetente);
        mime.To.Add(MailboxAddress.Parse(mensagem.Destinatario));
        mime.Subject = mensagem.Assunto;
        mime.MessageId = $"<{mensagem.MensagemId:N}@{_remetente.Domain}>";
        mime.Body = new BodyBuilder
        {
            TextBody = mensagem.CorpoTexto,
            HtmlBody = mensagem.CorpoHtml,
        }.ToMessageBody();

        return mime;
    }

    private static SecureSocketOptions Seguranca(SegurancaSmtp seguranca) => seguranca switch
    {
        SegurancaSmtp.Nenhuma => SecureSocketOptions.None,
        SegurancaSmtp.StartTls => SecureSocketOptions.StartTls,
        SegurancaSmtp.TlsImplicito => SecureSocketOptions.SslOnConnect,
        _ => throw new ArgumentOutOfRangeException(nameof(seguranca), seguranca, null),
    };
}
