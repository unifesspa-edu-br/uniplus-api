namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="OrigemValoresColeta"/> e o token canônico da fonte dos
/// valores, o mesmo que o catálogo de fatos grava em <c>fonte_valores</c>; o campo cujos valores
/// vêm do próprio catálogo usa <see cref="Catalogo"/>.
/// </summary>
public static class OrigemValoresColetaCodigo
{
    public const string Catalogo = "CATALOGO";
    public const string OpcoesDoProcesso = "PROCESSO";
    public const string MunicipiosDoBonus = "MUNICIPIOS_BONUS";
    public const string UnidadesFederativas = "GEO_UF";

    public static string ToCodigo(this OrigemValoresColeta origem) => origem switch
    {
        OrigemValoresColeta.Catalogo => Catalogo,
        OrigemValoresColeta.OpcoesDoProcesso => OpcoesDoProcesso,
        OrigemValoresColeta.MunicipiosDoBonus => MunicipiosDoBonus,
        OrigemValoresColeta.UnidadesFederativas => UnidadesFederativas,
        _ => throw new ArgumentOutOfRangeException(nameof(origem), origem, "Origem dos valores desconhecida."),
    };
}
