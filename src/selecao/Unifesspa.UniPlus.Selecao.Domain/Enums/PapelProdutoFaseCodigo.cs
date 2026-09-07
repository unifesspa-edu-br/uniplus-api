namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="PapelProdutoFase"/> e o token textual (UPPER_SNAKE) que o
/// contrato REST publica — fonte única do parsing na escrita e da projeção na leitura.
/// </summary>
/// <remarks>
/// Nunca comparar a string crua no handler nem emitir <c>enum.ToString()</c> no contrato,
/// que produziria <c>Preliminar</c> onde o contrato declara <c>PRELIMINAR</c>. A forma
/// canônica do envelope é outra e usa o nome do enum, como já fazem <c>origemData</c> e
/// <c>prazoUnidade</c>.
/// </remarks>
public static class PapelProdutoFaseCodigo
{
    public const string Preliminar = "PRELIMINAR";
    public const string Definitivo = "DEFINITIVO";

    /// <summary>
    /// Converte o token do contrato para o enum local. Texto ausente ou em branco é o
    /// produto <b>sem papel</b>, estado legítimo — devolve <see langword="true"/> com
    /// <paramref name="papel"/> nulo.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> apenas quando há um token e ele não é reconhecido. Devolver
    /// "sem papel" nesse caso transformaria um erro de digitação do operador num produto
    /// silenciosamente sem papel — e a fase deixaria de produzir resultado sem que nada
    /// acusasse.
    /// </returns>
    public static bool TentarConverter(string? codigo, out PapelProdutoFase? papel)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            papel = null;
            return true;
        }

        switch (codigo)
        {
            case Preliminar:
                papel = PapelProdutoFase.Preliminar;
                return true;
            case Definitivo:
                papel = PapelProdutoFase.Definitivo;
                return true;
            default:
                papel = null;
                return false;
        }
    }

    /// <summary>Converte o enum local de volta ao token do contrato; nulo projeta nulo.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se <paramref name="papel"/> tem um valor que não é declarável — encontrá-lo numa
    /// projeção denuncia coluna corrompida, não um caso a projetar.
    /// </exception>
    public static string? ToCodigo(this PapelProdutoFase? papel) => papel switch
    {
        null => null,
        PapelProdutoFase.Preliminar => Preliminar,
        PapelProdutoFase.Definitivo => Definitivo,
        _ => throw new ArgumentOutOfRangeException(nameof(papel), papel, "PapelProdutoFase desconhecido."),
    };
}
