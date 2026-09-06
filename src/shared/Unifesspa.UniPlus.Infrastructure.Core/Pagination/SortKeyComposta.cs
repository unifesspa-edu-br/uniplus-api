namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Globalization;

/// <summary>
/// Serializa e desserializa a chave de ordenação de um keyset cuja ordem é
/// definida por <b>mais de uma coluna de texto</b> antes do <c>Id</c> de
/// desempate (ADR-0094). O <c>CursorPayload.SortKey</c> é uma string única, e
/// esta classe é o formato que cabe várias partes nela.
/// </summary>
/// <remarks>
/// <para>Cada parte viaja prefixada pelo próprio comprimento
/// (<c>"&lt;n&gt;:&lt;n caracteres&gt;"</c>), de modo que o conteúdo é lido por
/// contagem e nunca interpretado: um separador que apareça dentro do texto não
/// desloca a leitura. Um delimitador simples (uma vírgula, um caractere de
/// controle) não teria essa propriedade — nada impede um nome de curso de
/// conter o caractere escolhido.</para>
/// <para>A desserialização é estrita: qualquer desvio do formato devolve
/// <see langword="false"/>, para o boundary tratar como cursor adulterado (400)
/// em vez de silenciosamente paginar do lugar errado.</para>
/// </remarks>
public static class SortKeyComposta
{
    /// <summary>Serializa as partes na ordem em que compõem a chave de ordenação.</summary>
    public static string Serializar(params string[] partes)
    {
        ArgumentNullException.ThrowIfNull(partes);

        return string.Concat(partes.Select(static parte =>
        {
            ArgumentNullException.ThrowIfNull(parte);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{parte.Length}:{parte}");
        }));
    }

    /// <summary>
    /// Lê exatamente <paramref name="quantidade"/> partes de
    /// <paramref name="sortKey"/>. Devolve <see langword="false"/> quando a
    /// string não é um serializado íntegro com essa quantidade — inclusive
    /// quando sobra conteúdo depois da última parte.
    /// </summary>
    public static bool TentarDesserializar(
        string? sortKey,
        int quantidade,
        out IReadOnlyList<string> partes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantidade);

        partes = [];

        if (sortKey is null)
        {
            return false;
        }

        List<string> lidas = new(quantidade);
        ReadOnlySpan<char> restante = sortKey;

        for (int i = 0; i < quantidade; i++)
        {
            int separador = restante.IndexOf(':');
            if (separador <= 0)
            {
                return false;
            }

            if (!int.TryParse(
                    restante[..separador],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int comprimento))
            {
                return false;
            }

            restante = restante[(separador + 1)..];
            if (comprimento > restante.Length)
            {
                return false;
            }

            lidas.Add(restante[..comprimento].ToString());
            restante = restante[comprimento..];
        }

        if (!restante.IsEmpty)
        {
            return false;
        }

        partes = lidas;
        return true;
    }
}
