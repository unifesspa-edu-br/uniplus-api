namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="CriterioDeConsolidacaoDoConceito"/> e o token textual que a regra de
/// consolidação declara no <c>declaracao_da_regra</c> — fonte única da leitura.
/// </summary>
public static class CriterioDeConsolidacaoDoConceitoCodigo
{
    public const string Unanimidade = "unanimidade";
    public const string MaioriaSimples = "maioria_simples";
    public const string MaioriaAbsoluta = "maioria_absoluta";

    private static readonly Dictionary<string, CriterioDeConsolidacaoDoConceito> PorCodigo =
        Enum.GetValues<CriterioDeConsolidacaoDoConceito>().ToDictionary(static criterio => criterio.ToCodigo(), StringComparer.Ordinal);

    /// <summary>
    /// Converte o token para o enum. Texto ausente, em branco ou desconhecido é recusado: um erro de digitação
    /// não pode passar por critério declarado.
    /// </summary>
    public static bool TentarConverter(string? codigo, out CriterioDeConsolidacaoDoConceito criterio)
    {
        criterio = default;

        return codigo is not null && PorCodigo.TryGetValue(codigo, out criterio);
    }

    /// <summary>Converte o enum de volta ao token.</summary>
    /// <exception cref="ArgumentOutOfRangeException">O valor não é declarável.</exception>
    public static string ToCodigo(this CriterioDeConsolidacaoDoConceito criterio) => criterio switch
    {
        CriterioDeConsolidacaoDoConceito.Unanimidade => Unanimidade,
        CriterioDeConsolidacaoDoConceito.MaioriaSimples => MaioriaSimples,
        CriterioDeConsolidacaoDoConceito.MaioriaAbsoluta => MaioriaAbsoluta,
        _ => throw new ArgumentOutOfRangeException(nameof(criterio), criterio, "CriterioDeConsolidacaoDoConceito desconhecido."),
    };
}
