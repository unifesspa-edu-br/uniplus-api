namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Unifesspa.UniPlus.Application.Abstractions.Email;

/// <summary>
/// Entrega a mensagem ao relay. Falha LANÇA: só a exceção faz o Wolverine reenviar.
/// </summary>
public interface ITransporteDeEmail
{
    Task EnviarAsync(EnviarEmail mensagem, CancellationToken cancellationToken);

    /// <summary>
    /// Entrega e avisa no instante em que o relay aceita a mensagem, antes do encerramento da sessão: quem mede o
    /// atraso até o aceite não conta o QUIT. Sem sessão a encerrar, o aceite é o fim da entrega.
    /// </summary>
    async Task EnviarAsync(EnviarEmail mensagem, Action aoAceitar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aoAceitar);

        await EnviarAsync(mensagem, cancellationToken).ConfigureAwait(false);
        aoAceitar();
    }
}
