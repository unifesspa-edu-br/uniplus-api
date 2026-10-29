namespace Unifesspa.UniPlus.Application.Abstractions.Email;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Pedido de envio de e-mail institucional. É a única porta de saída de e-mail dos módulos: quem
/// precisa avisar alguém devolve esta mensagem como cascading message do handler (ADR-0005) ou a
/// publica pelo <c>IMessageBus</c> dentro da transação — o envelope entra no outbox junto da
/// gravação que o motivou (ADR-0004) e o módulo nunca conhece o relay.
/// </summary>
/// <remarks>
/// O <see cref="Destinatario"/> vem sempre de dado confiável — o e-mail verificado do token ou o
/// cadastro —, nunca de texto livre da requisição. Não existe endpoint de envio; a garantia está em
/// cada chamador. Trafega como texto porque o envelope é serializado; use <see cref="Para"/> para
/// montar a partir do <see cref="Email"/> já validado.
/// </remarks>
/// <param name="MensagemId">Identifica o pedido no log e na reentrega.</param>
/// <param name="Destinatario">Endereço já validado e normalizado.</param>
/// <param name="Assunto">Assunto da mensagem. Nunca vai para o log.</param>
/// <param name="CorpoTexto">Versão em texto simples. Nunca vai para o log.</param>
/// <param name="CorpoHtml">Versão em HTML. Nunca vai para o log.</param>
/// <param name="Finalidade">Texto curto e estável que descreve o motivo do envio (ex.: <c>instalacao.codigo</c>); é o que o log registra.</param>
public sealed record EnviarEmail(
    Guid MensagemId,
    string Destinatario,
    string Assunto,
    string CorpoTexto,
    string CorpoHtml,
    string Finalidade)
{
    public static EnviarEmail Para(
        Email destinatario,
        string assunto,
        string corpoTexto,
        string corpoHtml,
        string finalidade)
    {
        ArgumentNullException.ThrowIfNull(destinatario);
        ArgumentException.ThrowIfNullOrWhiteSpace(assunto);
        ArgumentException.ThrowIfNullOrWhiteSpace(corpoTexto);
        ArgumentException.ThrowIfNullOrWhiteSpace(corpoHtml);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalidade);

        return new EnviarEmail(Guid.CreateVersion7(), destinatario.Valor, assunto, corpoTexto, corpoHtml, finalidade);
    }
}
