namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O token (UPPER_SNAKE) de <see cref="SituacaoDoDocumento"/> no contrato da avaliação da inscrição.</summary>
public static class SituacaoDoDocumentoCodigo
{
    public const string Exigido = "EXIGIDO";
    public const string PodeSerExigido = "PODE_SER_EXIGIDO";
    public const string DependeDeFasePosterior = "DEPENDE_DE_FASE_POSTERIOR";

    /// <summary>
    /// O token da situação de uma exigência que se aplica ou pode se aplicar ao candidato. A que não se
    /// aplica não chega ao contrato do candidato, e por isso não tem token.
    /// </summary>
    public static string ToCodigo(this SituacaoDoDocumento situacao) => situacao switch
    {
        SituacaoDoDocumento.Exigido => Exigido,
        SituacaoDoDocumento.PodeSerExigido => PodeSerExigido,
        SituacaoDoDocumento.DependeDeFasePosterior => DependeDeFasePosterior,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "Situação do documento fora do contrato do candidato."),
    };
}
