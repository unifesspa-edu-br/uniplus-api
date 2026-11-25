namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
        return LerLista(leitor, array, pathRecursos);
    }

    private static List<RecursoDaEtapa>? LerLista(LeitorEnvelope leitor, JsonArray? array, string pathRecursos)
    {
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
        leitor.ExigirChaves(itemRecurso, pathRecurso, "eventoInicialDoPrazo", "regra", "parametros", "publicacaoDeReferenciaId", "procedimentos");
        EventoInicialDoPrazo ancora = leitor.CodigoDeContrato<EventoInicialDoPrazo>(itemRecurso, "eventoInicialDoPrazo", pathRecurso);

        // A referência vem inteira e conferida contra o código de regra esperado.
        ReferenciaRegra regra = leitor.Regra<RegraDePrazoDoRecurso>(itemRecurso, "regra", pathRecurso);

        // A âncora por ciência não tem produto: o campo vem nulo.
        Guid? ancoraId = leitor.IdentificadorOpcional(itemRecurso, "publicacaoDeReferenciaId", pathRecurso);
        JsonObject parametrosObjeto = leitor.Objeto(itemRecurso, "parametros", pathRecurso);
        ProcedimentosDeRecurso? procedimentos = LerProcedimentos(leitor, itemRecurso, pathRecurso);
        return leitor.Falhou || procedimentos is null
            ? null
            : Reidratar(leitor, new(ancora, regra, ancoraId, procedimentos), parametrosObjeto, pathRecurso);
    }

    private static ProcedimentosDeRecurso? LerProcedimentos(LeitorEnvelope leitor, JsonObject item, string path) =>
        leitor.Falhou ? null : LeituraDosProcedimentosDeRecurso.Ler(leitor, item, path, CodigosDoProcedimentoDeRecurso.DaEtapa);

    private sealed record RecursoLido(EventoInicialDoPrazo Ancora, ReferenciaRegra Regra, Guid? AncoraId, ProcedimentosDeRecurso Procedimentos);

    private static RecursoDaEtapa? Reidratar(LeitorEnvelope leitor, RecursoLido lido, JsonObject parametrosObjeto, string pathRecurso)
    {
        ParametrosDeclaradosDoRecurso? parametros = LeituraDosPrazosDeRecurso.LerParametrosDePrazoDeRecurso(leitor, parametrosObjeto, $"{pathRecurso}.parametros");
        if (leitor.Falhou || parametros is null)
        {
            return null;
        }

        return Montar(leitor, lido, parametros, pathRecurso);
    }

    private static RecursoDaEtapa? Montar(LeitorEnvelope leitor, RecursoLido lido, ParametrosDeclaradosDoRecurso parametros, string pathRecurso)
    {
        Result<RecursoDaEtapa> recurso = RecursoDaEtapa.Reidratar(
            Guid.CreateVersion7(), lido.Ancora, lido.Regra, parametros, lido.AncoraId ?? Guid.Empty, lido.Procedimentos);
        return recurso.IsFailure
            ? leitor.Propagar<RecursoDaEtapa?>(recurso.Errors, pathRecurso)
            : recurso.Value;
    }
}
