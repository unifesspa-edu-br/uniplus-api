namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

using System;

/// <summary>
/// Interrompe a preparação de um lote sem perder o que já havia sido classificado.
/// </summary>
/// <remarks>
/// Preparar um lote é decidir, vínculo a vínculo, se ele entra, muda ou já está igual na
/// réplica — e cifrar o CPF dos dois primeiros casos. Uma falha no meio disso não apaga as
/// decisões já tomadas: os vínculos reconhecidos como iguais continuam corretos na réplica,
/// e contá-los como não gravados faria o registro da execução subestimar o que ela alcançou.
/// A contagem parcial viaja junto da falha justamente para não ficar presa aqui dentro.
/// </remarks>
public sealed class FalhaAoPrepararLoteException : Exception
{
    public FalhaAoPrepararLoteException(ResultadoDaGravacao parcial, Exception causa)
        : base("Falha ao preparar o lote de vínculos para gravação.", causa)
    {
        Parcial = parcial;
    }

    public FalhaAoPrepararLoteException()
        : this(new ResultadoDaGravacao(0, 0, 0), new InvalidOperationException())
    {
    }

    public FalhaAoPrepararLoteException(string message)
        : base(message) => Parcial = new ResultadoDaGravacao(0, 0, 0);

    public FalhaAoPrepararLoteException(string message, Exception innerException)
        : base(message, innerException) => Parcial = new ResultadoDaGravacao(0, 0, 0);

    /// <summary>O que já havia sido classificado quando a falha aconteceu.</summary>
    public ResultadoDaGravacao Parcial { get; } = new(0, 0, 0);
}
