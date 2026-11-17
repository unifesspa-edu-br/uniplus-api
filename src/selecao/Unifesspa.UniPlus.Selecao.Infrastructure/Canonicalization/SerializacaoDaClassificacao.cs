namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope as regras de classificação: desempate, quadro de pesos por área do Enem e eliminação.
/// </summary>
internal static class SerializacaoDaClassificacao
{
    internal static JsonArray SerializarCriteriosDesempate(ProcessoSeletivo processo)
    {
        JsonArray array = [];
        foreach (CriterioDesempate criterio in processo.CriteriosDesempate.OrderBy(static c => c.Ordem))
        {
            array.Add(new JsonObject
            {
                ["ordem"] = criterio.Ordem,
                ["regra"] = EscritaCanonica.SerializarReferenciaRegra(criterio.Regra),
                ["parametros"] = SerializarParametrosCriterioDesempate(criterio.Parametros),
            });
        }

        return array;
    }

    private static JsonObject SerializarParametrosCriterioDesempate(ParametrosCriterioDesempate parametros) => parametros switch
    {
        ParametrosDesempateMaiorNotaEtapa maiorNotaEtapa => new JsonObject { ["etapaRef"] = maiorNotaEtapa.EtapaRef },
        ParametrosDesempateMaiorIdade => [],
        ParametrosDesempateIdoso idoso => new JsonObject { ["idadeMinima"] = idoso.IdadeMinima },
        ParametrosDesempatePredicadoFato predicadoFato => new JsonObject
        {
            ["fato"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Condicao.Fato),
            ["operador"] = predicadoFato.Condicao.Operador.ToCodigo(),
            ["valor"] = SerializacaoDosPredicados.SerializarValorDeAtomo(predicadoFato.Condicao.Operador, predicadoFato.Condicao.Valor),
        },
        ParametrosDesempateMaiorNotaAreaEnem maiorNotaAreaEnem => new JsonObject
        {
            ["areas"] = new JsonArray([.. maiorNotaAreaEnem.Areas.Select(static area => (JsonNode?)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(area)))]),
        },
        _ => throw new InvalidOperationException($"Variante de {nameof(ParametrosCriterioDesempate)} não reconhecida: {parametros.GetType()}."),
    };

    internal static JsonObject SerializarClassificacao(ProcessoSeletivo processo)
    {
        if (processo.Classificacao is not { } classificacao)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem configuração de classificação — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["regraCalculo"] = EscritaCanonica.SerializarReferenciaRegra(classificacao.RegraCalculo),
            ["regraArredondamento"] = classificacao.RegraArredondamento is { } arredondamento
                ? EscritaCanonica.SerializarReferenciaRegra(arredondamento)
                : null,
            ["casasArredondamento"] = classificacao.CasasArredondamento,
            ["escalaNotaFinal"] = classificacao.EscalaNotaFinal is { } escalaNotaFinal ? HashCanonicalComputer.SerializeDecimalCanonical(escalaNotaFinal, ConfiguracaoClassificacao.EscalaNotaFinalEscala) : null,
            ["regraOrdemAlocacao"] = EscritaCanonica.SerializarReferenciaRegra(classificacao.RegraOrdemAlocacao),
            ["baseadoEmEnem"] = classificacao.BaseadoEmEnem,
            ["regrasEliminacao"] = EscritaCanonica.OrdenarPorConteudo(classificacao.RegrasEliminacao
                .Select(static r => new JsonObject
                {
                    ["regra"] = EscritaCanonica.SerializarReferenciaRegra(r.Regra),
                    ["parametros"] = SerializarParametrosRegraEliminacao(r.Parametros),
                })),
            ["resolucaoPesoAreaEnem"] = classificacao.ResolucaoPesoAreaEnem is { } resolucao
                ? HashCanonicalComputer.NormalizeNfc(resolucao)
                : null,
            ["quadroPesoAreaEnem"] = SerializarQuadroPesoAreaEnem(classificacao.QuadroPesoAreaEnem),
        };
    }

    private static JsonArray SerializarQuadroPesoAreaEnem(IEnumerable<GrupoPesoAreaEnemCongelado> quadro) =>
        new([.. quadro
            .OrderBy(static g => HashCanonicalComputer.NormalizeNfc(g.GrupoAreaEnem.Codigo), StringComparer.Ordinal)
            .Select(static g => (JsonNode)new JsonObject
            {
                ["grupoAreaEnem"] = SerializacaoDasVagas.SerializarGrupoAreaEnem(g.GrupoAreaEnem),
                ["baseLegal"] = HashCanonicalComputer.NormalizeNfc(g.BaseLegal),
                ["areas"] = new JsonArray([.. g.Areas
                    .OrderBy(static a => HashCanonicalComputer.NormalizeNfc(a.Codigo), StringComparer.Ordinal)
                    .Select(static a => (JsonNode)new JsonObject
                    {
                        ["codigo"] = HashCanonicalComputer.NormalizeNfc(a.Codigo),
                        ["rotulo"] = HashCanonicalComputer.NormalizeNfc(a.Rotulo),
                        ["peso"] = HashCanonicalComputer.SerializeDecimalCanonical(a.Peso, EscritaCanonica.EscalaPadrao),
                        ["corte"] = a.Corte is { } corte ? HashCanonicalComputer.SerializeDecimalCanonical(corte, EscritaCanonica.EscalaPadrao) : null,
                    })]),
            })]);

    private static JsonObject SerializarParametrosRegraEliminacao(ParametrosRegraEliminacao parametros) => parametros switch
    {
        ParametrosElimAusenciaNaEtapa ausencia => new JsonObject { ["etapaRef"] = ausencia.EtapaRef },
        ParametrosElimNotaMinimaEtapa notaMinima => new JsonObject
        {
            ["etapaRef"] = notaMinima.EtapaRef,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscritaCanonica.EscalaPadrao),
        },
        ParametrosElimCorteEmArea corteEmArea => new JsonObject
        {
            ["areaCodigo"] = HashCanonicalComputer.NormalizeNfc(corteEmArea.AreaCodigo),
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteEmArea.Minimo, EscritaCanonica.EscalaPadrao),
        },
        ParametrosElimZeroEmArea or ParametrosElimFaltaEmDiaDeProvaEnem => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ParametrosRegraEliminacao)} não reconhecida: {parametros.GetType()}."),
    };
}
