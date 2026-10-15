namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Um limite da faixa de data (UNI-REQ-0145): uma data fixa, ou o dia do envio deslocado em anos e
/// dias — "até o envio" é o envio sem deslocamento, e "120 anos antes do envio" é o envio menos 120
/// anos.
/// </summary>
/// <remarks>
/// O deslocamento soma primeiro os anos e depois os dias, e o 29 de fevereiro que cai em ano sem ele
/// vira 28, como em <see cref="DateOnly.AddYears(int)"/>: é essa a conta que o interpretador do front
/// reproduz. O teto do deslocamento deixa de fora o limite que nenhuma data plausível alcança e mantém
/// a conta dentro do calendário.
/// </remarks>
public sealed record LimiteDeData
{
    /// <summary>O maior deslocamento em anos, para mais ou para menos.</summary>
    public const int TetoDeAnos = 150;

    /// <summary>O maior deslocamento em dias, para mais ou para menos: o mesmo intervalo do teto em anos.</summary>
    public const int TetoDeDias = TetoDeAnos * 366;

    private LimiteDeData(AncoraDoLimiteDeData ancora, DateOnly? data, int anos, int dias)
    {
        Ancora = ancora;
        Data = data;
        Anos = anos;
        Dias = dias;
    }

    public AncoraDoLimiteDeData Ancora { get; }

    /// <summary>A data do limite fixo; nula no limite contado do envio.</summary>
    public DateOnly? Data { get; }

    /// <summary>Os anos somados ao dia do envio; zero no limite fixo.</summary>
    public int Anos { get; }

    /// <summary>Os dias somados ao dia do envio, depois dos anos; zero no limite fixo.</summary>
    public int Dias { get; }

    public static LimiteDeData Fixo(DateOnly data) =>
        ViolacaoDaDataFixa(data) is { } violacao
            ? throw new ArgumentException(violacao, nameof(data))
            : new(AncoraDoLimiteDeData.DataFixa, data, 0, 0);

    public static LimiteDeData NoEnvio(int anos, int dias) =>
        ViolacaoDoDeslocamento(anos, dias) is { } violacao
            ? throw new ArgumentException(violacao)
            : new(AncoraDoLimiteDeData.Envio, null, anos, dias);

    /// <summary>O dia do limite; nulo quando ele conta do envio e o dia do envio não se sabe.</summary>
    public DateOnly? No(DateOnly? diaDoEnvio) =>
        Ancora == AncoraDoLimiteDeData.DataFixa ? Data : diaDoEnvio?.AddYears(Anos).AddDays(Dias);

    /// <summary>
    /// Se o limite cai depois do outro seja qual for o dia do envio. Entre dois contados do envio, o que
    /// desloca ao menos tanto nos anos e nos dias, e mais em um deles, cai depois, porque somar anos e
    /// depois dias nunca recua o dia. Nulo quando a ordem depende do dia do envio: um fixo e outro
    /// contado do envio, ou dois contados do envio que deslocam mais em sentidos opostos — mais anos e
    /// menos dias.
    /// </summary>
    public bool? DepoisDe(LimiteDeData outro)
    {
        ArgumentNullException.ThrowIfNull(outro);
        return (Ancora, outro.Ancora) switch
        {
            (AncoraDoLimiteDeData.DataFixa, AncoraDoLimiteDeData.DataFixa) => Data > outro.Data,
            (AncoraDoLimiteDeData.Envio, AncoraDoLimiteDeData.Envio) when Anos >= outro.Anos && Dias >= outro.Dias =>
                Anos > outro.Anos || Dias > outro.Dias,
            (AncoraDoLimiteDeData.Envio, AncoraDoLimiteDeData.Envio) when Anos <= outro.Anos && Dias <= outro.Dias => false,
            _ => null,
        };
    }

    /// <summary>
    /// Se o dia do envio comporta qualquer limite contado dele: o deslocamento máximo, para os dois
    /// lados, ainda cai dentro do calendário.
    /// </summary>
    public static bool ComportaOsDeslocamentos(DateOnly diaDoEnvio) =>
        diaDoEnvio >= DateOnly.MinValue.AddYears(TetoDeAnos).AddDays(TetoDeDias)
        && diaDoEnvio <= DateOnly.MaxValue.AddYears(-TetoDeAnos).AddDays(-TetoDeDias);

    /// <summary>
    /// O primeiro dia do calendário não é limite: é como uma data omitida se materializa, e o
    /// envelope congelado o recusa pelo mesmo motivo.
    /// </summary>
    internal static string? ViolacaoDaDataFixa(DateOnly data) =>
        data == default ? "A data fixa do limite não pode ser 01/01/0001, a forma de uma data omitida." : null;

    internal static string? ViolacaoDoDeslocamento(int anos, int dias) =>
        anos is < -TetoDeAnos or > TetoDeAnos || dias is < -TetoDeDias or > TetoDeDias
            ? $"O deslocamento do dia do envio vai até {TetoDeAnos} anos ou {TetoDeDias} dias, para mais ou para menos."
            : null;
}
