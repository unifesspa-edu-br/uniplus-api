namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Confere a cascata informada contra a declaração da versão da regra.</summary>
public static class ConformidadeDaCascataComARegra
{
    /// <summary>Compara o destino final, as origens e a ordem dos destinos.</summary>
    public static bool Conferir(ConfiguracaoCascataRemanejamento cascata, RegraCatalogo regra)
    {
        ArgumentNullException.ThrowIfNull(cascata);
        ArgumentNullException.ThrowIfNull(regra);
        JsonElement declaracao = regra.DeclaracaoDaRegra;
        return declaracao.TryGetProperty("destinoFinalCodigo", out JsonElement destinoFinal)
            && string.Equals(destinoFinal.GetString(), cascata.FallbackCodigo, StringComparison.Ordinal)
            && declaracao.TryGetProperty("ordens", out JsonElement ordens)
            && ordens.ValueKind == JsonValueKind.Array
            && ConferirOrigens(cascata, ordens);
    }

    private static bool ConferirOrigens(ConfiguracaoCascataRemanejamento cascata, JsonElement ordens)
    {
        Dictionary<string, List<string>> destinos = DestinosPorOrigem(cascata);
        HashSet<string> conferidas = new(StringComparer.Ordinal);
        foreach (JsonElement origem in ordens.EnumerateArray())
        {
            if (!ConferirOrigem(origem, destinos, conferidas))
            {
                return false;
            }
        }

        return conferidas.SetEquals(destinos.Keys);
    }

    private static Dictionary<string, List<string>> DestinosPorOrigem(ConfiguracaoCascataRemanejamento cascata) =>
        cascata.Destinos.GroupBy(static d => d.ModalidadeOrigemCodigo, StringComparer.Ordinal)
            .ToDictionary(static grupo => grupo.Key,
                static grupo => grupo.OrderBy(static d => d.Ordem).Select(static d => d.ModalidadeDestinoCodigo).ToList(),
                StringComparer.Ordinal);

    private static bool ConferirOrigem(
        JsonElement item, Dictionary<string, List<string>> destinos, HashSet<string> conferidas)
    {
        if (!item.TryGetProperty("origem", out JsonElement origemElemento)
            || !item.TryGetProperty("destinos", out JsonElement destinosElemento)
            || destinosElemento.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        string? origem = origemElemento.GetString();
        return origem is not null && conferidas.Add(origem)
            && destinos.TryGetValue(origem, out List<string>? esperados)
            && ConferirDestinos(destinosElemento, esperados);
    }

    private static bool ConferirDestinos(JsonElement declarados, List<string> esperados) =>
        declarados.EnumerateArray().Select(static d => d.GetString()).SequenceEqual(esperados, StringComparer.Ordinal);
}
