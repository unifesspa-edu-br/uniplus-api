namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// O ponto de continuação que veio no cursor não corresponde à consulta: a chave
/// está ausente, malformada ou é de outra ordenação, filtro, busca ou recorte.
/// </summary>
/// <remarks>
/// O caso que se espera na prática é reapresentar um cursor sob outra consulta
/// que não aquela em que foi emitido — a página seguinte de uma listagem
/// com um filtro não continua a listagem com outro. Vira
/// 400 na borda, e não 500: quem escolheu a combinação foi o cliente.
/// </remarks>
public sealed class CursorAnchorMismatchException : Exception
{
    public CursorAnchorMismatchException()
        : base("O ponto de continuação do cursor não corresponde à consulta pedida.")
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
