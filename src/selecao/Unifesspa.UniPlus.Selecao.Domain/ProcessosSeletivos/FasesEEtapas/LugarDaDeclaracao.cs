namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using System.Globalization;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Como o texto para o operador diz onde uma declaração está: a fase pelo nome, e a etapa pelo nome e pela ordem, porque o nome não é único.</summary>
internal static class LugarDaDeclaracao
{
    internal static string DaFase(CitacaoDaFase fase) => $"na fase {fase}";

    internal static string DaEtapa(string nome, int? ordem) =>
        $"na etapa '{nome}' (ordem {ordem?.ToString(CultureInfo.InvariantCulture) ?? "não definida"})";
}
