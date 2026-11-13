namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Unifesspa.UniPlus.Application.Abstractions.Email;

/// <summary>
/// Entrega a mensagem ao relay. Falha LANÇA: só a exceção faz o Wolverine reenviar.
/// </summary>
public interface ITransporteDeEmail
{
    /// <summary>
    /// Entrega e chama <paramref name="aoAceitar"/> no instante em que o relay aceita a mensagem, antes do encerramento
    /// da sessão: quem mede o atraso até o aceite não conta o QUIT.
    /// </summary>
    Task EnviarAsync(EnviarEmail mensagem, Action aoAceitar, CancellationToken cancellationToken);
}
