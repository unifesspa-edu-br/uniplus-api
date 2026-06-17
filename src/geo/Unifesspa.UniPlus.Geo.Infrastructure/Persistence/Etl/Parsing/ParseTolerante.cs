namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Parsing;

using System.Globalization;

/// <summary>
/// Conversores tolerantes do ETL DNE (ADR-0092). A fonte é toda <c>varchar</c> e
/// traz <c>'-'</c>/vazio para dado ausente (≈27% dos municípios em
/// <c>mortalidade_infantil</c>): toda métrica externa é <c>nullable</c> e
/// <c>'-'</c>/vazio/não-numérico <strong>degrada para <see langword="null"/> sem
/// lançar</strong>, para a carga nunca abortar por dado sujo. <see cref="CultureInfo.InvariantCulture"/>
/// sempre — a fonte usa ponto como separador decimal.
/// </summary>
internal static class ParseTolerante
{
    // Sinal + ponto decimal apenas. SEM AllowThousands: a fonte usa ponto como
    // separador decimal e não tem milhar; aceitar vírgula faria "6,52" virar 652
    // (corrompendo o dado) em vez de degradar — queremos rejeitar e cair para null.
    private const NumberStyles EstiloDecimal = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    /// <summary>Converte para <see cref="decimal"/> tolerante; ausência/inválido → <see langword="null"/>.</summary>
    public static decimal? ParaDecimal(string? valor) =>
        decimal.TryParse(Limpar(valor), EstiloDecimal, CultureInfo.InvariantCulture, out decimal numero)
            ? numero
            : null;

    /// <summary>Converte para <see cref="int"/> tolerante; ausência/inválido → <see langword="null"/>.</summary>
    public static int? ParaInteiro(string? valor) =>
        int.TryParse(Limpar(valor), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numero)
            ? numero
            : null;

    /// <summary>Mapeia o indicador <c>'S'</c>/<c>'N'</c> da DNE para <see cref="bool"/> (só <c>'S'</c> é verdadeiro).</summary>
    public static bool ParaBoolSn(string? valor) =>
        string.Equals(Limpar(valor), "S", StringComparison.OrdinalIgnoreCase);

    // Trim; '-' (sentinela de ausência da DNE) e vazio/branco viram null antes do TryParse.
    private static string? Limpar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        string limpo = valor.Trim();
        return limpo == "-" ? null : limpo;
    }
}
