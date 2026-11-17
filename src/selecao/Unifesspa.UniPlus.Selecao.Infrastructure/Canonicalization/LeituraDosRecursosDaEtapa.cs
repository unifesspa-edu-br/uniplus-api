namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das janelas de recurso de uma etapa do envelope: a âncora, a regra de prazo e os parâmetros dela.
/// </summary>
internal static class LeituraDosRecursosDaEtapa
{
    internal static List<RecursoDaEtapa>? Ler(LeitorEnvelope leitor, JsonObject item, string path)
    {
        JsonArray? array = leitor.Array(item, "recursos", path);
        string pathRecursos = $"{path}.recursos";
        List<RecursoDaEtapa> recursos = [];
        for (int j = 0; array is not null && j < array.Count; j++)
        {
            RecursoDaEtapa? recurso = LerRecurso(leitor, leitor.ItemObjeto(array, j, pathRecursos), $"{pathRecursos}[{j}]");
            if (recurso is null)
            {
                return null;
            }

            recursos.Add(recurso);
        }

        return recursos;
    }

    private static RecursoDaEtapa? LerRecurso(LeitorEnvelope leitor, JsonObject itemRecurso, string pathRecurso)
    {
        leitor.ExigirChaves(itemRecurso, pathRecurso, "ancora", "regra", "parametros", "produtoAncoraId", "procedimentos");
        AncoraDoRecurso ancora = leitor.Enumeracao<AncoraDoRecurso>(itemRecurso, "ancora", pathRecurso);

        // A referência vem inteira e conferida contra o código de regra esperado.
        ReferenciaRegra regra = leitor.Regra(itemRecurso, "regra", pathRecurso, RegraPrazoRecursoCodigo.AncoradoEmAto);

        // A âncora por ciência não tem produto: o campo vem nulo.
        Guid? ancoraId = leitor.IdentificadorOpcional(itemRecurso, "produtoAncoraId", pathRecurso);
        JsonObject parametrosObjeto = leitor.Objeto(itemRecurso, "parametros", pathRecurso);
        ProcedimentosDeRecurso? procedimentos = leitor.Falhou
            ? null
            : LeituraDosProcedimentosDeRecurso.Ler(leitor, itemRecurso, pathRecurso, CodigosDoProcedimentoDeRecurso.DaEtapa);
        return leitor.Falhou || procedimentos is null
            ? null
            : Reidratar(leitor, new(ancora, regra, ancoraId, procedimentos), parametrosObjeto, pathRecurso);
    }

    private sealed record RecursoLido(AncoraDoRecurso Ancora, ReferenciaRegra Regra, Guid? AncoraId, ProcedimentosDeRecurso Procedimentos);

    private static RecursoDaEtapa? Reidratar(LeitorEnvelope leitor, RecursoLido lido, JsonObject parametrosObjeto, string pathRecurso)
    {
        (AncoraDoRecurso ancora, ReferenciaRegra regra, Guid? ancoraId, ProcedimentosDeRecurso procedimentos) = lido;
        // Nenhuma é o sentinela de ausência que RecursoDaEtapa.Criar recusa e que Reidratar não reconfere.
        if (ancora == AncoraDoRecurso.Nenhuma)
        {
            return leitor.Malformado<RecursoDaEtapa?>($"{pathRecurso}.ancora", "o recurso tem de declarar de que instante o prazo corre.");
        }

        ParametrosRegraPrazoRecurso? parametros = LeituraDosPrazosDeRecurso.LerParametrosDePrazoDeRecurso(leitor, parametrosObjeto, $"{pathRecurso}.parametros");
        if (leitor.Falhou || parametros is null)
        {
            return null;
        }

        // Identidade nova: o que a publicação promete é a janela, não a linha que a guarda.
        Result<RecursoDaEtapa> recurso = RecursoDaEtapa.Reidratar(Guid.CreateVersion7(), ancora, regra, parametros, ancoraId ?? Guid.Empty, procedimentos);
        return recurso.IsFailure
            ? leitor.Propagar<RecursoDaEtapa?>(recurso.Error!)
            : recurso.Value;
    }
}
