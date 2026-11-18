namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using System.Globalization;

/// <summary>Como o texto para o operador diz onde uma declaração está: a fase pelo código, e a etapa pelo nome e pela ordem, porque o nome não é único.</summary>
internal static class LugarDaDeclaracao
{
    internal static string DaFase(string codigo) => $"na fase '{codigo}'";

    internal static string DaEtapa(string nome, int? ordem) =>
        $"na etapa '{nome}' (ordem {ordem?.ToString(CultureInfo.InvariantCulture) ?? "não definida"})";
}
