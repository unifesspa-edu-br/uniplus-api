namespace Unifesspa.UniPlus.Infrastructure.Core.Email;

using Unifesspa.UniPlus.Application.Abstractions.Email;

/// <summary>
/// Entrega a mensagem ao relay. Falha LANÇA: só a exceção faz o Wolverine reenviar.
/// </summary>
public interface ITransporteDeEmail
{
    Task EnviarAsync(EnviarEmail mensagem, CancellationToken cancellationToken);
}
