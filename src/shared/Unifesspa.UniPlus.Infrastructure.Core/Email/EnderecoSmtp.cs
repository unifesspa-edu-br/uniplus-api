namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using MimeKit;

/// <summary>
/// Regra única do que o transporte aceita como endereço: addr-spec puro com domínio, sem nome de
/// exibição. Remetente e destinatário passam por aqui para que a checagem na subida e no
/// handler coincida com o que o <c>MailboxAddress</c> do transporte aceita ou recusa.
/// </summary>
internal static class EnderecoSmtp
{
    public static bool EhAddrSpecComDominio(string? texto) =>
        !string.IsNullOrWhiteSpace(texto)
        && MailboxAddress.TryParse(texto, out MailboxAddress? endereco)
        && string.IsNullOrEmpty(endereco.Name)
        && !string.IsNullOrEmpty(endereco.Domain)
        && string.Equals(endereco.Address, texto, StringComparison.Ordinal);
}
