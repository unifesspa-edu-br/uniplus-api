namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// A âncora que veio no cursor não corresponde à ordenação da consulta: a chave
/// tem um número de colunas diferente do pedido, ou não está no formato que a
/// ordenação serializa.
/// </summary>
/// <remarks>
/// O caso que se espera na prática é reapresentar um cursor sob uma ordenação
/// diferente daquela em que foi emitido — a página seguinte de uma listagem
/// ordenada por um critério não continua uma listagem ordenada por outro. Vira
/// 400 na borda, e não 500: quem escolheu a combinação foi o cliente.
/// </remarks>
public sealed class CursorAnchorMismatchException : Exception
{
    public CursorAnchorMismatchException()
        : base("A âncora do cursor não corresponde à ordenação pedida.")
    {
    }

    public CursorAnchorMismatchException(string message)
        : base(message)
    {
    }

    public CursorAnchorMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
