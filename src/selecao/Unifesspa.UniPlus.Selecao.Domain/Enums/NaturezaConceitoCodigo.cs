namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="NaturezaConceito"/> e o token textual que o catálogo de tipos de conceito
/// publica e que a regra de consolidação declara — fonte única da leitura e da projeção.
/// </summary>
public static class NaturezaConceitoCodigo
{
    public const string Numerico = "numerico";
    public const string Categorico = "categorico";

    /// <summary>
    /// Converte o token para o enum. Texto ausente, em branco ou desconhecido é recusado: o conceito sempre
    /// tem natureza, e tratar o token desconhecido como ausente faria um erro de digitação passar por
    /// conceito sem natureza.
    /// </summary>
    public static bool TentarConverter(string? codigo, out NaturezaConceito natureza)
    {
        switch (codigo)
        {
            case Numerico:
                natureza = NaturezaConceito.Numerico;
                return true;
            case Categorico:
                natureza = NaturezaConceito.Categorico;
                return true;
            default:
                natureza = default;
                return false;
        }
    }

    /// <summary>Converte o enum de volta ao token.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se <paramref name="natureza"/> tem um valor que não é declarável — encontrá-lo numa projeção denuncia
    /// dado corrompido, não um caso a projetar.
    /// </exception>
    public static string ToCodigo(this NaturezaConceito natureza) => natureza switch
    {
        NaturezaConceito.Numerico => Numerico,
        NaturezaConceito.Categorico => Categorico,
        _ => throw new ArgumentOutOfRangeException(nameof(natureza), natureza, "NaturezaConceito desconhecida."),
    };
}
