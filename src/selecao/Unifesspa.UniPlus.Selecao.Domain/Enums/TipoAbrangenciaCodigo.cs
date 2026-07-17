namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoAbrangencia"/> e o código textual canônico
/// UPPER_SNAKE do wire de comando (mesma convenção de <see cref="OperadorCodigo"/>) —
/// fonte de verdade única do wire format. Também usado por <c>.HasConversion&lt;string&gt;()</c>
/// na persistência (Story #554, PR #898).
/// </summary>
public static class TipoAbrangenciaCodigo
{
    public const string Federal = "FEDERAL";
    public const string Estadual = "ESTADUAL";
    public const string Municipal = "MUNICIPAL";
    public const string InternaNorma = "INTERNA_NORMA";
    public const string InternaEdital = "INTERNA_EDITAL";

    public static string ToCodigo(this TipoAbrangencia abrangencia) => abrangencia switch
    {
        TipoAbrangencia.Federal => Federal,
        TipoAbrangencia.Estadual => Estadual,
        TipoAbrangencia.Municipal => Municipal,
        TipoAbrangencia.InternaNorma => InternaNorma,
        TipoAbrangencia.InternaEdital => InternaEdital,
        TipoAbrangencia.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(abrangencia), abrangencia, "TipoAbrangencia.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(abrangencia), abrangencia, "TipoAbrangencia desconhecido."),
    };

    public static TipoAbrangencia FromCodigo(string? codigo) => codigo switch
    {
        Federal => TipoAbrangencia.Federal,
        Estadual => TipoAbrangencia.Estadual,
        Municipal => TipoAbrangencia.Municipal,
        InternaNorma => TipoAbrangencia.InternaNorma,
        InternaEdital => TipoAbrangencia.InternaEdital,
        _ => TipoAbrangencia.Nenhuma,
    };
}
