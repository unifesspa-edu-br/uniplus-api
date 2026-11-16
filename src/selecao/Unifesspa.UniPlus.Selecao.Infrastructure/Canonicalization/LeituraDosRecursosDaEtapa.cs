namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das janelas de recurso de uma etapa do envelope: a âncora, a regra de prazo e os argumentos dela.
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
        leitor.ExigirChaves(itemRecurso, pathRecurso, "ancora", "regra", "args", "produtoAncoraId");
        AncoraDoRecurso ancora = leitor.Enumeracao<AncoraDoRecurso>(itemRecurso, "ancora", pathRecurso);

        // A referência vem inteira e conferida contra o código de regra esperado.
        ReferenciaRegra regra = leitor.Regra(itemRecurso, "regra", pathRecurso, RegraPrazoRecursoCodigo.AncoradoEmAto);

        // A âncora por ciência não tem produto: o campo vem nulo.
        Guid? ancoraId = leitor.IdentificadorOpcional(itemRecurso, "produtoAncoraId", pathRecurso);
        JsonObject argsObjeto = leitor.Objeto(itemRecurso, "args", pathRecurso);
        return leitor.Falhou ? null : Reidratar(leitor, ancora, regra, ancoraId, argsObjeto, pathRecurso);
    }

    private static RecursoDaEtapa? Reidratar(
        LeitorEnvelope leitor, AncoraDoRecurso ancora, ReferenciaRegra regra, Guid? ancoraId, JsonObject argsObjeto, string pathRecurso)
    {
        // Nenhuma é o sentinela de ausência que RecursoDaEtapa.Criar recusa e que Reidratar não reconfere.
        if (ancora == AncoraDoRecurso.Nenhuma)
        {
            return leitor.Malformado<RecursoDaEtapa?>($"{pathRecurso}.ancora", "o recurso tem de declarar de que instante o prazo corre.");
        }

        ArgsRegraPrazoRecurso? args = LeituraDosPrazosDeRecurso.LerArgsDePrazoDeRecurso(leitor, argsObjeto, $"{pathRecurso}.args");
        if (leitor.Falhou || args is null)
        {
            return null;
        }

        // Identidade nova: o que a publicação promete é a janela, não a linha que a guarda.
        Result<RecursoDaEtapa> recurso = RecursoDaEtapa.Reidratar(Guid.CreateVersion7(), ancora, regra, args, ancoraId ?? Guid.Empty);
        return recurso.IsFailure
            ? leitor.Propagar<RecursoDaEtapa?>(recurso.Error!)
            : recurso.Value;
    }
}
