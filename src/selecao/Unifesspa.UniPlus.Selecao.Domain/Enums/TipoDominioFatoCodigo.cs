namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoDominioFato"/> e o código textual
/// canônico UPPER_SNAKE que entra no envelope canônico de publicação
/// (ADR-0111, Story #847). Fonte de verdade única do wire format.
/// </summary>
public static class TipoDominioFatoCodigo
{
    public const string Booleano = "BOOLEANO";
    public const string Numerico = "NUMERICO";
    public const string CategoricoEstatico = "CATEGORICO_ESTATICO";

    /// <summary>
    /// Converte o tipo para o código canônico. O <c>switch</c> é exaustivo:
    /// um 4º domínio quebra a build (CS8509 promovido a erro por
    /// <c>TreatWarningsAsErrors</c>) até este mapeamento absorvê-lo.
    /// </summary>
    public static string ToCodigo(this TipoDominioFato tipo) => tipo switch
    {
        TipoDominioFato.Booleano => Booleano,
        TipoDominioFato.Numerico => Numerico,
        TipoDominioFato.CategoricoEstatico => CategoricoEstatico,
        TipoDominioFato.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "TipoDominioFato.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoDominioFato desconhecido."),
    };

    /// <summary>
    /// Converte o código canônico de volta para o tipo. Um código não
    /// reconhecido mapeia para o sentinela <see cref="TipoDominioFato.Nenhuma"/>.
    /// </summary>
    public static TipoDominioFato FromCodigo(string? codigo) => codigo switch
    {
        Booleano => TipoDominioFato.Booleano,
        Numerico => TipoDominioFato.Numerico,
        CategoricoEstatico => TipoDominioFato.CategoricoEstatico,
        _ => TipoDominioFato.Nenhuma,
    };
}
