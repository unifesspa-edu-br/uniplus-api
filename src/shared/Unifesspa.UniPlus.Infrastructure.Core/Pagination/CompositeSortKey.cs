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
public static class CompositeSortKey
{
    /// <summary>Serializa as partes na ordem em que compõem a chave de ordenação.</summary>
    public static string Serialize(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return string.Concat(parts.Select(static part =>
        {
            ArgumentNullException.ThrowIfNull(part);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{part.Length}:{part}");
        }));
    }

    /// <summary>
    /// Lê exatamente <paramref name="count"/> partes de
    /// <paramref name="sortKey"/>. Devolve <see langword="false"/> quando a
    /// string não é um serializado íntegro com essa quantidade — inclusive
    /// quando sobra conteúdo depois da última parte.
    /// </summary>
    public static bool TryDeserialize(
        string? sortKey,
        int count,
        out IReadOnlyList<string> parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        parts = [];

        if (sortKey is null)
        {
            return false;
        }

        List<string> read = new(count);
        ReadOnlySpan<char> remaining = sortKey;

        for (int i = 0; i < count; i++)
        {
            int separator = remaining.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            if (!int.TryParse(
                    remaining[..separator],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int length))
            {
                return false;
            }

            remaining = remaining[(separator + 1)..];
            if (length > remaining.Length)
            {
                return false;
            }

            read.Add(remaining[..length].ToString());
            remaining = remaining[length..];
        }

        if (!remaining.IsEmpty)
        {
            return false;
        }

        parts = read;
        return true;
    }
}
