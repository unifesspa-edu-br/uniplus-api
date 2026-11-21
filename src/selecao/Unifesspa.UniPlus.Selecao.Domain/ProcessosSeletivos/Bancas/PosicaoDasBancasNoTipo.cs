namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// A posição de cada banca entre as do mesmo tipo, a partir de 1, na ordem em que o lugar as recebe.
/// </summary>
internal static class PosicaoDasBancasNoTipo
{
    internal static void Numerar<TBanca>(IReadOnlyList<TBanca> bancasEmOrdemDeChegada)
        where TBanca : class, IBancaDoLugar<TBanca>
    {
        IReadOnlyList<int> posicoes = Calcular(bancasEmOrdemDeChegada.Select(static banca => banca.Tipo.Codigo));
        for (int indice = 0; indice < posicoes.Count; indice++)
        {
            bancasEmOrdemDeChegada[indice].PosicionarNoTipo(posicoes[indice]);
        }
    }

    /// <summary>A posição de cada tipo entre os do mesmo tipo, na ordem em que chegam.</summary>
    internal static IReadOnlyList<int> Calcular(IEnumerable<TipoDeBanca> tiposEmOrdemDeChegada)
    {
        Dictionary<TipoDeBanca, int> jaNumeradasPorTipo = [];

        return [.. tiposEmOrdemDeChegada.Select(tipo => jaNumeradasPorTipo[tipo] = jaNumeradasPorTipo.GetValueOrDefault(tipo) + 1)];
    }
}
