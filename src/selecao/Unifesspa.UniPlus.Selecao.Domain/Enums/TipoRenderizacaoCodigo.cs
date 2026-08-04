namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento único entre <see cref="TipoRenderizacao"/> e o código textual canônico
/// UPPER_SNAKE que entra no envelope canônico de publicação e no wire de comando — mesmo
/// raciocínio de <see cref="OperadorCodigo"/>. Fonte de verdade única do wire format.
/// </summary>
public static class TipoRenderizacaoCodigo
{
    public const string Numero = "NUMERO";
    public const string Booleano = "BOOLEANO";
    public const string SelecaoUnica = "SELECAO_UNICA";
    public const string SelecaoMultipla = "SELECAO_MULTIPLA";

    /// <summary>
    /// Converte para o código canônico. O <c>switch</c> é exaustivo: um 5º valor quebra a build
    /// (CS8509 promovido a erro por <c>TreatWarningsAsErrors</c>) até este mapeamento absorvê-lo.
    /// </summary>
    public static string ToCodigo(this TipoRenderizacao tipo) => tipo switch
    {
        TipoRenderizacao.Numero => Numero,
        TipoRenderizacao.Booleano => Booleano,
        TipoRenderizacao.SelecaoUnica => SelecaoUnica,
        TipoRenderizacao.SelecaoMultipla => SelecaoMultipla,
        TipoRenderizacao.Nenhuma => throw new ArgumentOutOfRangeException(
            nameof(tipo), tipo, "TipoRenderizacao.Nenhuma é sentinela e não tem código canônico."),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoRenderizacao desconhecido."),
    };

    /// <summary>
    /// Converte o código canônico de volta para o tipo. Um código não reconhecido (ou ausente —
    /// o campo é omitido do JSON) mapeia para o sentinela <see cref="TipoRenderizacao.Nenhuma"/>,
    /// que <see cref="Entities.FatoColetado.Criar"/> já rejeita com um erro de domínio (422)
    /// claro, em vez de aceitar silenciosamente um tipo de renderização não informado.
    /// </summary>
    public static TipoRenderizacao FromCodigo(string? codigo) => codigo switch
    {
        Numero => TipoRenderizacao.Numero,
        Booleano => TipoRenderizacao.Booleano,
        SelecaoUnica => TipoRenderizacao.SelecaoUnica,
        SelecaoMultipla => TipoRenderizacao.SelecaoMultipla,
        _ => TipoRenderizacao.Nenhuma,
    };
}
