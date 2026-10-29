namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Logging;

using MimeKit;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Consome <see cref="EnviarEmail"/> da fila durável e entrega pelo <see cref="ITransporteDeEmail"/>.
/// </summary>
/// <remarks>
/// O nome não termina em "Handler" de propósito: a descoberta por convenção varre este assembly
/// compartilhado em todo host, e o Portal não registra o transporte. Quem chama
/// <c>AddEnvioDeEmail</c> inclui o tipo explicitamente (<c>Discovery.IncludeType</c>).
/// </remarks>
/// <remarks>
/// O log, emitido pela própria classe, registra só a finalidade, o id, a tentativa e o destinatário mascarado — nunca assunto
/// nem corpo, que podem carregar código de confirmação. Falha LANÇA: um retorno de erro seria lido
/// pelo Wolverine como sucesso e a mensagem sairia da fila sem ter sido enviada.
/// </remarks>
public sealed partial class EntregaDeEmail(ITransporteDeEmail transporte, ILogger<EntregaDeEmail> logger)
{
    /// <summary>
    /// Política de falha DESTA mensagem, escopada à chain: uma política global atrasaria toda
    /// falha de validação dos commands HTTP, que também passam por retry inline.
    /// </summary>
    public static void Configure(HandlerChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        // Recusa permanente não melhora com o tempo — endereço inválido, rejeição 5xx, credencial
        // recusada, mensagem que não monta: vai direto para a fila de mensagens mortas.
        chain.OnException<DestinatarioInvalidoException>().MoveToErrorQueue();
        chain.OnException<EnvioDeEmailRecusadoException>().MoveToErrorQueue();

        // Retry em linha só cobre o soluço; queda de relay dura mais, e segurar a fila durante ela
        // atrasaria os demais e-mails. Depois do curto, reagenda em intervalos crescentes e só então
        // manda para a fila de mensagens mortas.
        chain.OnException<Exception>()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
            .Then.ScheduleRetry(
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1))
            .Then.MoveToErrorQueue();
    }

    public async Task Handle(EnviarEmail mensagem, Envelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(envelope);

        // Mesmo parser do transporte: o que passa aqui é o que o MailboxAddress aceita lá.
        if (!EnderecoSmtp.EhAddrSpecComDominio(mensagem.Destinatario)
            || Email.Criar(mensagem.Destinatario) is not { IsSuccess: true } destinatario)
        {
            LogDestinatarioInvalido(logger, mensagem.Finalidade, mensagem.MensagemId);
            throw new DestinatarioInvalidoException(mensagem.MensagemId);
        }

        string mascarado = destinatario.Value!.Mascarado;
        int tentativa = envelope.Attempts;

        try
        {
            await transporte.EnviarAsync(mensagem, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            string tipoDoErro = ex.GetType().Name;

            // A exceção original some de propósito: a mensagem do SMTP costuma repetir o endereço, e
            // o Wolverine a grava no log e na tabela de mensagens mortas.
            if (EhPermanente(ex))
            {
                LogEnvioRecusado(logger, mensagem.Finalidade, mensagem.MensagemId, tipoDoErro);
                throw new EnvioDeEmailRecusadoException(mensagem.MensagemId, tipoDoErro);
            }

            LogFalhaNoEnvio(logger, mensagem.Finalidade, mensagem.MensagemId, tentativa, mascarado, tipoDoErro);
            throw new FalhaNoEnvioDeEmailException(mensagem.MensagemId, tipoDoErro);
        }

        LogEnviado(logger, mensagem.Finalidade, mensagem.MensagemId, tentativa, mascarado);
    }

    // Falha de autenticação fica com as novas tentativas: o MailKit não expõe se o relay
    // respondeu 4xx (indisponível) ou 5xx (credencial errada), e credencial errada esgota as
    // tentativas e cai na fila de mensagens mortas do mesmo jeito.
    private static bool EhPermanente(Exception ex) => ex is
        SmtpCommandException { StatusCode: >= (SmtpStatusCode)500 } or ParseException;

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Relay recusou o e-mail em definitivo: finalidade {Finalidade}, mensagem {MensagemId}, erro {TipoDoErro}")]
    private static partial void LogEnvioRecusado(ILogger logger, string finalidade, Guid mensagemId, string tipoDoErro);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "E-mail enviado: finalidade {Finalidade}, mensagem {MensagemId}, tentativa {Tentativa}, destinatário {Destinatario}")]
    private static partial void LogEnviado(ILogger logger, string finalidade, Guid mensagemId, int tentativa, string destinatario);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Pedido de e-mail recusado por destinatário inválido: finalidade {Finalidade}, mensagem {MensagemId}")]
    private static partial void LogDestinatarioInvalido(ILogger logger, string finalidade, Guid mensagemId);

    // Só o tipo da exceção: a mensagem de erro do SMTP costuma repetir o endereço sem máscara.
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao enviar e-mail: finalidade {Finalidade}, mensagem {MensagemId}, tentativa {Tentativa}, destinatário {Destinatario}, erro {TipoDoErro}")]
    private static partial void LogFalhaNoEnvio(
        ILogger logger, string finalidade, Guid mensagemId, int tentativa, string destinatario, string tipoDoErro);
}

/// <summary>O pedido de envio chegou com um destinatário que não é e-mail válido.</summary>
public sealed class DestinatarioInvalidoException : Exception
{
    public DestinatarioInvalidoException(Guid mensagemId)
        : base($"O destinatário do pedido de e-mail {mensagemId} não é um endereço válido.")
    {
    }

    public DestinatarioInvalidoException()
    {
    }

    public DestinatarioInvalidoException(string message)
        : base(message)
    {
    }

    public DestinatarioInvalidoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A entrega falhou. Carrega só o id do pedido e o tipo do erro original, para o endereço não
/// chegar ao log do Wolverine nem à tabela de mensagens mortas.
/// </summary>
public sealed class FalhaNoEnvioDeEmailException : Exception
{
    public FalhaNoEnvioDeEmailException(Guid mensagemId, string tipoDoErro)
        : base($"Falha ao enviar o pedido de e-mail {mensagemId} ({tipoDoErro}).")
    {
    }

    public FalhaNoEnvioDeEmailException()
    {
    }

    public FalhaNoEnvioDeEmailException(string message)
        : base(message)
    {
    }

    public FalhaNoEnvioDeEmailException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// O relay ou a montagem recusou o pedido de forma permanente; reenviar não resolve. Carrega só o
/// id e o tipo do erro, pelo mesmo motivo de <see cref="FalhaNoEnvioDeEmailException"/>.
/// </summary>
public sealed class EnvioDeEmailRecusadoException : Exception
{
    public EnvioDeEmailRecusadoException(Guid mensagemId, string tipoDoErro)
        : base($"Pedido de e-mail {mensagemId} recusado em definitivo ({tipoDoErro}).")
    {
    }

    public EnvioDeEmailRecusadoException()
    {
    }

    public EnvioDeEmailRecusadoException(string message)
        : base(message)
    {
    }

    public EnvioDeEmailRecusadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
