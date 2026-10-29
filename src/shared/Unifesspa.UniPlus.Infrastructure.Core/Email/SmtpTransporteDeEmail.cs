namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using System.Net.Sockets;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MimeKit;

using Unifesspa.UniPlus.Application.Abstractions.Email;

public sealed partial class SmtpTransporteDeEmail : ITransporteDeEmail
{
    private readonly EmailSmtpOptions _opcoes;
    private readonly ILogger<SmtpTransporteDeEmail> _logger;
    private readonly MailboxAddress _remetente;

    public SmtpTransporteDeEmail(IOptions<EmailSmtpOptions> options, ILogger<SmtpTransporteDeEmail> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _opcoes = options.Value;
        _logger = logger;
        _remetente = new MailboxAddress(_opcoes.NomeRemetente, _opcoes.Remetente);
    }

    public async Task EnviarAsync(EnviarEmail mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        using MimeMessage mime = Montar(mensagem);

        // Uma conexão por mensagem: o volume é baixo e evita reaproveitar sessão que o relay já derrubou.
        using SmtpClient cliente = new();
        await cliente.ConnectAsync(_opcoes.Host, _opcoes.Porta, Seguranca(_opcoes.Seguranca), cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(_opcoes.Usuario))
        {
            await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await cliente.SendAsync(mime, cancellationToken).ConfigureAwait(false);

        // O relay já aceitou a mensagem: falhar o QUIT não pode virar falha de envio, senão o
        // Wolverine reenvia e o destinatário recebe duas vezes.
        try
        {
            await cliente.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
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
