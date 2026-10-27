namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Mapeamento entre <see cref="FormaDeAvaliacao"/> e o token textual (UPPER_SNAKE) que o
/// contrato REST publica — fonte única do parsing na escrita e da projeção na leitura. A forma
/// canônica do envelope é outra e usa o nome do enum.
/// </summary>
public static class FormaDeAvaliacaoCodigo
{
    public const string Objetiva = "OBJETIVA";
    public const string Humana = "HUMANA";

    /// <summary>
    /// Converte o token do contrato para o enum. Texto ausente ou em branco é a etapa sem forma
    /// declarada, estado legítimo em rascunho — devolve <see langword="true"/> com
    /// <paramref name="forma"/> nulo.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> apenas quando há um token e ele não é reconhecido: tratá-lo como
    /// ausente faria um erro de digitação passar por etapa sem forma.
    /// </returns>
    public static bool TentarConverter(string? codigo, out FormaDeAvaliacao? forma)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            forma = null;
            return true;
        }

        switch (codigo)
        {
            case Objetiva:
                forma = FormaDeAvaliacao.Objetiva;
                return true;
            case Humana:
                forma = FormaDeAvaliacao.Humana;
                return true;
            default:
                forma = null;
                return false;
        }
    }

    /// <summary>Converte o enum de volta ao token do contrato; nulo projeta nulo.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se <paramref name="forma"/> tem um valor que não é declarável — encontrá-lo numa projeção
    /// denuncia coluna corrompida, não um caso a projetar.
    /// </exception>
    public static string? ToCodigo(this FormaDeAvaliacao? forma) => forma switch
    {
        null => null,
        FormaDeAvaliacao.Objetiva => Objetiva,
        FormaDeAvaliacao.Humana => Humana,
        _ => throw new ArgumentOutOfRangeException(nameof(forma), forma, "FormaDeAvaliacao desconhecida."),
    };
}
