namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using System.Text;

/// <summary>
/// A forma de um texto para comparação: sem acento e em minúsculas. É o que as colunas geradas
/// de ordenação guardam no banco e o que a busca compara contra elas.
/// </summary>
/// <remarks>
/// <para>A tabela de substituição vive aqui, num lugar só, e a função do banco
/// (<c>plataforma.normalizar_para_comparacao</c>) a repete caractere a caractere: a coluna é gerada
/// pelo banco e o termo pesquisado é normalizado pela aplicação antes de virar um <c>LIKE</c>
/// contra ela. Tabelas divergentes dariam uma busca que não acha o que existe, sem erro algum.</para>
/// <para>A cobertura é a das letras acentuadas do português, não a do Unicode inteiro: o que está
/// fora da tabela passa intacto. Basta para nome, código e número de edital.</para>
/// </remarks>
public static class FormaParaComparacao
{
    /// <summary>Letras acentuadas reconhecidas, na ordem em que se substituem.</summary>
    private const string Acentuadas =
        "ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý";

    /// <summary>Equivalentes sem acento, posição a posição.</summary>
    private const string SemAcento =
        "AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny";

    /// <summary>
    /// Normaliza o texto na ordem da função do banco: forma Unicode composta, troca das
    /// acentuadas, minúsculas invariantes.
    /// </summary>
    public static string De(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        string composto = texto.Normalize(NormalizationForm.FormC);
        StringBuilder trocado = new(composto.Length);

        foreach (char caractere in composto)
        {
            int posicao = Acentuadas.IndexOf(caractere, StringComparison.Ordinal);
            trocado.Append(posicao >= 0 ? SemAcento[posicao] : caractere);
        }

        return trocado.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// O texto pesquisado na forma de comparação, ou <see langword="null"/> quando não há busca:
    /// texto ausente, vazio ou em branco é o mesmo que nenhum. O caractere nulo é descartado antes
    /// de tudo, porque texto no Postgres não admite byte zero e nada que alguém queira procurar o
    /// contém; depois o texto é aparado e normalizado por <see cref="De"/>.
    /// </summary>
    public static string? Termo(string? texto)
    {
        if (texto is null)
        {
            return null;
        }

        string semNulo = texto.Replace("\0", string.Empty, StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(semNulo) ? null : De(semNulo.Trim());
    }
}
