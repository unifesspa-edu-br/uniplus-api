namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos critérios de desempate e de seus argumentos.
/// </summary>
internal static class LeituraDoDesempate
{
    internal static IReadOnlyList<CriterioDesempate> LerCriteriosDesempate(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "criteriosDesempate", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<CriterioDesempate> criterios = [];
        for (int i = 0; i < array.Count; i++)
        {
            CriterioDesempate? criterio = LerCriterio(leitor, leitor.ItemObjeto(array, i, "criteriosDesempate"), $"criteriosDesempate[{i}]");
            if (criterio is null)
            {
                return [];
            }

            criterios.Add(criterio);
        }

        return criterios;
    }

    private static CriterioDesempate? LerCriterio(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "ordem", "regra", "args");

        int ordem = leitor.Inteiro(item, "ordem", path);
        ReferenciaRegra regra = leitor.Regra(
            item,
            "regra",
            path,
            CriterioDesempateCodigo.MaiorNotaEtapa,
            CriterioDesempateCodigo.MaiorIdade,
            CriterioDesempateCodigo.Idoso,
            CriterioDesempateCodigo.PredicadoFato,
            CriterioDesempateCodigo.MaiorNotaAreaEnem);
        JsonObject args = leitor.Objeto(item, "args", path);
        if (leitor.Falhou)
        {
            return null;
        }

        ArgsCriterioDesempate? argumentos = LerArgsDesempate(leitor, regra.Codigo, args, $"{path}.args");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<CriterioDesempate> criterio = CriterioDesempate.Criar(ordem, regra, argumentos!);
        return criterio.IsFailure ? leitor.Propagar<CriterioDesempate?>(criterio.Error!) : criterio.Value;
    }

    /// <summary>
    /// A variante vem do <b>código da regra</b> — o envelope não carrega discriminador, e
    /// duas variantes distintas (<c>MAIOR-IDADE</c>, <c>ZERO-EM-AREA</c>) serializam ambas
    /// como <c>{}</c>. Exigir o objeto vazio <b>exatamente</b> é o que impede que args de
    /// uma variante entrem em outra sem que ninguém veja.
    /// </summary>
    internal static ArgsCriterioDesempate? LerArgsDesempate(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject args,
        string path) => codigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => LerMaiorNotaEtapa(leitor, args, path),
            CriterioDesempateCodigo.MaiorIdade => LerMaiorIdade(leitor, args, path),
            CriterioDesempateCodigo.Idoso => LerIdoso(leitor, args, path),
            CriterioDesempateCodigo.PredicadoFato => LerPredicadoFato(leitor, args, path),
            CriterioDesempateCodigo.MaiorNotaAreaEnem => LerMaiorNotaAreaEnem(leitor, args, path),
            _ => leitor.Propagar<ArgsCriterioDesempate>(new DomainError(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"Não há variante de args conhecida para o critério de desempate '{codigo}' em '{path}'.")),
        };

    private static ArgsDesempateMaiorNotaEtapa? LerMaiorNotaEtapa(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "etapaRef");
        Guid etapaRef = leitor.Identificador(args, "etapaRef", path);
        return leitor.Falhou ? null : new ArgsDesempateMaiorNotaEtapa(etapaRef);
    }

    private static ArgsDesempateMaiorIdade? LerMaiorIdade(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path);
        return leitor.Falhou ? null : new ArgsDesempateMaiorIdade();
    }

    private static ArgsDesempateIdoso? LerIdoso(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "idadeMinima");
        int idadeMinima = leitor.Inteiro(args, "idadeMinima", path);
        return leitor.Falhou ? null : new ArgsDesempateIdoso(idadeMinima);
    }

    // A forma (fato não vazio, operador reconhecido, valor coerente com o operador) é conferida pela mesma
    // factory que o caminho de comando usa. O decoder não revalida a semântica, porque o congelamento dos
    // parâmetros na publicação proíbe reinterpretar um predicado já congelado contra um catálogo que
    // pode ter mudado.
    private static ArgsCriterioDesempate? LerPredicadoFato(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "fato", "operador", "valor");
        string fato = leitor.TextoNaoVazio(args, "fato", path);
        string operadorCodigo = leitor.TextoNaoVazio(args, "operador", path);
        System.Text.Json.JsonElement valor = leitor.Valor(args, "valor", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<CondicaoDnf> condicao = CondicaoDnf.Criar(fato, OperadorCodigo.FromCodigo(operadorCodigo), valor);
        return condicao.IsFailure
            ? leitor.Propagar<ArgsCriterioDesempate>(condicao.Error!)
            : new ArgsDesempatePredicadoFato(condicao.Value!);
    }

    // A forma (ao menos uma área, código válido, sem repetição) é conferida por CriterioDesempate.Criar; a
    // existência de cada área no quadro congelado, pela restauração do grafo.
    private static ArgsDesempateMaiorNotaAreaEnem? LerMaiorNotaAreaEnem(LeitorEnvelope leitor, JsonObject args, string path)
    {
        leitor.ExigirChaves(args, path, "areas");
        IReadOnlyList<string> areas = leitor.Textos(args, "areas", path);
        return leitor.Falhou ? null : new ArgsDesempateMaiorNotaAreaEnem(areas);
    }
}
