namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// As bancas do mesmo tipo que atendem os mesmos candidatos: julgam em paralelo o mesmo conjunto de candidatos. O conjunto é o
/// das categorias de documento que a banca declara, comparadas pelo código e sem importar a ordem; as bancas sem categoria
/// atendem todos os candidatos e formam um grupo só.
/// </summary>
/// <param name="Tipo">O tipo das bancas do grupo.</param>
/// <param name="Categorias">Os códigos das categorias de documento, sem repetição e em ordem; vazio quando atendem todos os candidatos.</param>
/// <param name="Posicoes">A posição de cada banca do grupo na lista em que foram agrupadas, na ordem em que aparecem.</param>
internal sealed record GrupoDeCandidatosAtendidos(TipoDeBanca Tipo, IReadOnlyList<string> Categorias, IReadOnlyList<int> Posicoes)
{
    /// <summary>Se as bancas do grupo não declaram categoria e, por isso, atendem todos os candidatos.</summary>
    internal bool AtendeTodosOsCandidatos => Categorias.Count == 0;

    /// <summary>Os grupos, na ordem em que a primeira banca de cada um aparece.</summary>
    internal static IReadOnlyList<GrupoDeCandidatosAtendidos> Agrupar(IReadOnlyList<(TipoDeBanca Tipo, IReadOnlyList<string> Categorias)> bancas)
    {
        ArgumentNullException.ThrowIfNull(bancas);

        return [.. Enumerable.Range(0, bancas.Count)
            .GroupBy(posicao => new Chave(bancas[posicao].Tipo, bancas[posicao].Categorias))
            .Select(grupo => new GrupoDeCandidatosAtendidos(grupo.Key.Tipo, grupo.Key.Categorias, [.. grupo]))];
    }

    /// <summary>
    /// Os tipos em que há banca sem categoria ao lado de banca com categoria: a banca sem categoria atende todos os candidatos, e
    /// não se sabe então quais deles cada banca do tipo julga.
    /// </summary>
    internal static IReadOnlySet<TipoDeBanca> TiposComBancasComESemCategorias(IEnumerable<GrupoDeCandidatosAtendidos> grupos)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        return grupos
            .GroupBy(static grupo => grupo.Tipo)
            .Where(static doTipo => doTipo.Any(static grupo => grupo.AtendeTodosOsCandidatos) && doTipo.Any(static grupo => !grupo.AtendeTodosOsCandidatos))
            .Select(static doTipo => doTipo.Key)
            .ToHashSet();
    }

    private sealed record Chave(TipoDeBanca Tipo, IReadOnlyList<string> Categorias)
    {
        public bool Equals(Chave? outra) =>
            outra is not null && Tipo == outra.Tipo && Categorias.SequenceEqual(outra.Categorias, StringComparer.Ordinal);

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(Tipo);
            foreach (string categoria in Categorias)
            {
                hash.Add(categoria, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
