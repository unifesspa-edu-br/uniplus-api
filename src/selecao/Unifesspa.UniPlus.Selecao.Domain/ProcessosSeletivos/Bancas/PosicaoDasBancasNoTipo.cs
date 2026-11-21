namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A posição de cada banca entre as do mesmo tipo, a partir de 1, na ordem em que o lugar as recebe. O tipo é o código, em NFC,
/// como o documento canônico o emite.
/// </summary>
internal static class PosicaoDasBancasNoTipo
{
    internal static void Numerar<TBanca>(IEnumerable<TBanca> bancasEmOrdemDeChegada)
        where TBanca : class, IBancaDoLugar<TBanca>
    {
        Dictionary<string, int> jaNumeradasPorTipo = new(StringComparer.Ordinal);
        foreach (TBanca banca in bancasEmOrdemDeChegada)
        {
            string tipo = HashCanonicalComputer.NormalizeNfc(banca.Codigo);
            int posicao = jaNumeradasPorTipo.GetValueOrDefault(tipo) + 1;
            jaNumeradasPorTipo[tipo] = posicao;
            banca.PosicionarNoTipo(posicao);
        }
    }
}
