namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Registro de que um aviso foi emitido: o identificador da mensagem, que deriva do evento, do fato e da identidade do
/// destinatário, e o que o avisou. Não guarda endereço nem identidade em claro, só se acrescenta e nunca se altera
/// (<see cref="IForensicEntity"/>).
/// </summary>
internal sealed class AvisoEmitido : IForensicEntity
{
    /// <summary>O <c>MensagemId</c> do e-mail.</summary>
    public Guid Id { get; init; }

    public string Tipo { get; init; } = string.Empty;

    public Guid FatoId { get; init; }

    public DateTimeOffset EmitidoEm { get; init; }
}
