namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>O token (UPPER_SNAKE) de <see cref="SituacaoDasModalidades"/> no contrato da avaliação da inscrição.</summary>
public static class SituacaoDasModalidadesCodigo
{
    public const string Concorre = "CONCORRE";
    public const string NaoConcorre = "NAO_CONCORRE";
    public const string FaltaResponder = "FALTA_RESPONDER";

    /// <summary>O token da situação; nulo quando não há situação, porque a configuração não deriva a modalidade.</summary>
    public static string? ToCodigo(this SituacaoDasModalidades situacao) => situacao switch
    {
        SituacaoDasModalidades.Nenhuma => null,
        SituacaoDasModalidades.Concorre => Concorre,
        SituacaoDasModalidades.NaoConcorre => NaoConcorre,
        SituacaoDasModalidades.FaltaResponder => FaltaResponder,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "Situação das modalidades fora do vocabulário."),
    };
}
