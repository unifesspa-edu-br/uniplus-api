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
/// Leitura dos critérios de desempate e de seus parâmetros.
/// </summary>
internal static class LeituraDoDesempate
{
    private static readonly string[] CodigosDeCriterioAceitos =
    [
        CriterioDesempateCodigo.MaiorNotaEtapa,
        CriterioDesempateCodigo.MaiorIdade,
        CriterioDesempateCodigo.Idoso,
        CriterioDesempateCodigo.PredicadoFato,
        CriterioDesempateCodigo.MaiorNotaAreaEnem,
    ];

    internal static IReadOnlyList<CriterioDesempate> LerCriteriosDesempate(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "criteriosDesempate", "$");

        return leitor.Falhou ? [] : LerCriteriosDoArray(leitor, array);
    }

    private static List<CriterioDesempate> LerCriteriosDoArray(LeitorEnvelope leitor, JsonArray array)
    {
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
        leitor.ExigirChaves(item, path, "ordem", "regra", "parametros");
        int ordem = leitor.Inteiro(item, "ordem", path);
        ReferenciaRegra regra = leitor.Regra(item, "regra", path, CodigosDeCriterioAceitos);
        JsonObject parametros = leitor.Objeto(item, "parametros", path);

        return leitor.Falhou ? null : CriarCriterio(leitor, ordem, regra, parametros, $"{path}.parametros");
    }

    private static CriterioDesempate? CriarCriterio(
        LeitorEnvelope leitor, int ordem, ReferenciaRegra regra, JsonObject parametros, string path)
    {
        ParametrosCriterioDesempate? parametrosTipados = LerParametrosDesempate(leitor, regra.Codigo, parametros, path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<CriterioDesempate> criterio = CriterioDesempate.Criar(ordem, regra, parametrosTipados!);
        return criterio.IsFailure ? leitor.Propagar<CriterioDesempate?>(criterio.Error!) : criterio.Value;
    }

    /// <summary>
    /// A variante vem do <b>código da regra</b> — o envelope não carrega discriminador, e
    /// duas variantes distintas (<c>MAIOR-IDADE</c>, <c>ZERO-EM-AREA</c>) serializam ambas
    /// como <c>{}</c>. Exigir o objeto vazio <b>exatamente</b> é o que impede que parâmetros de
    /// uma variante entrem em outra sem que ninguém veja.
    /// </summary>
    internal static ParametrosCriterioDesempate? LerParametrosDesempate(
        LeitorEnvelope leitor,
        string codigo,
        JsonObject parametros,
        string path) => codigo switch
        {
            CriterioDesempateCodigo.MaiorNotaEtapa => LerMaiorNotaEtapa(leitor, parametros, path),
            CriterioDesempateCodigo.MaiorIdade => LerMaiorIdade(leitor, parametros, path),
            CriterioDesempateCodigo.Idoso => LerIdoso(leitor, parametros, path),
            CriterioDesempateCodigo.PredicadoFato => LerPredicadoFato(leitor, parametros, path),
            CriterioDesempateCodigo.MaiorNotaAreaEnem => LerMaiorNotaAreaEnem(leitor, parametros, path),
            _ => leitor.Propagar<ParametrosCriterioDesempate>(new DomainError(
                ErrosCodecEnvelope.RegraDesconhecida,
                $"Não há variante de parâmetros conhecida para o critério de desempate '{codigo}' em '{path}'.")),
        };

    private static ParametrosDesempateMaiorNotaEtapa? LerMaiorNotaEtapa(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "etapaRef");
        Guid etapaRef = leitor.Identificador(parametros, "etapaRef", path);
        return leitor.Falhou ? null : new ParametrosDesempateMaiorNotaEtapa(etapaRef);
    }

    private static ParametrosDesempateMaiorIdade? LerMaiorIdade(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path);
        return leitor.Falhou ? null : new ParametrosDesempateMaiorIdade();
    }

    private static ParametrosDesempateIdoso? LerIdoso(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "idadeMinima");
        int idadeMinima = leitor.Inteiro(parametros, "idadeMinima", path);
        return leitor.Falhou ? null : new ParametrosDesempateIdoso(idadeMinima);
    }

    // O operador é reconhecido por `LerOperador`; a forma (fato não vazio, valor coerente com o operador) é conferida
    // pela mesma factory que o caminho de comando usa. O decoder não revalida a semântica, porque o congelamento dos
    // parâmetros na publicação proíbe reinterpretar um predicado já congelado contra um catálogo que
    // pode ter mudado.
    private static ParametrosCriterioDesempate? LerPredicadoFato(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "fato", "operador", "valor");
        string fato = leitor.TextoNaoVazio(parametros, "fato", path);
        Operador operador = LeituraDasCondicoes.LerOperador(leitor, parametros, path);
        System.Text.Json.JsonElement valor = leitor.Valor(parametros, "valor", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<CondicaoDnf> condicao = CondicaoDnf.Criar(fato, operador, valor);
        return condicao.IsFailure
            ? leitor.Propagar<ParametrosCriterioDesempate>(condicao.Error!)
            : new ParametrosDesempatePredicadoFato(condicao.Value!);
    }

    // A forma (ao menos uma área, código válido, sem repetição) é conferida por CriterioDesempate.Criar; a
    // existência de cada área no quadro congelado, pela reidratação do grafo.
    private static ParametrosDesempateMaiorNotaAreaEnem? LerMaiorNotaAreaEnem(LeitorEnvelope leitor, JsonObject parametros, string path)
    {
        leitor.ExigirChaves(parametros, path, "areas");
        IReadOnlyList<string> areas = leitor.Textos(parametros, "areas", path);
        return leitor.Falhou ? null : new ParametrosDesempateMaiorNotaAreaEnem(areas);
    }
}
