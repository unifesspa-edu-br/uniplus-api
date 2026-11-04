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
                ["args"] = SerializarArgsCriterioDesempate(criterio.Args),
            });
        }

        return array;
    }

    private static JsonObject SerializarArgsCriterioDesempate(ArgsCriterioDesempate args) => args switch
    {
        ArgsDesempateMaiorNotaEtapa maiorNotaEtapa => new JsonObject { ["etapaRef"] = maiorNotaEtapa.EtapaRef },
        ArgsDesempateMaiorIdade => [],
        ArgsDesempateIdoso idoso => new JsonObject { ["idadeMinima"] = idoso.IdadeMinima },
        ArgsDesempatePredicadoFato predicadoFato => new JsonObject
        {
            ["fato"] = HashCanonicalComputer.NormalizeNfc(predicadoFato.Condicao.Fato),
            ["operador"] = predicadoFato.Condicao.Operador.ToCodigo(),
            ["valor"] = SerializacaoDosPredicados.SerializarValorDeAtomo(predicadoFato.Condicao.Operador, predicadoFato.Condicao.Valor),
        },
        ArgsDesempateMaiorNotaAreaEnem maiorNotaAreaEnem => new JsonObject
        {
            ["areas"] = new JsonArray([.. maiorNotaAreaEnem.Areas.Select(static area => (JsonNode?)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(area)))]),
        },
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsCriterioDesempate)} não reconhecida: {args.GetType()}."),
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
                    ["args"] = SerializarArgsRegraEliminacao(r.Args),
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

    private static JsonObject SerializarArgsRegraEliminacao(ArgsRegraEliminacao args) => args switch
    {
        ArgsElimAusenciaNaEtapa ausencia => new JsonObject { ["etapaRef"] = ausencia.EtapaRef },
        ArgsElimNotaMinimaEtapa notaMinima => new JsonObject
        {
            ["etapaRef"] = notaMinima.EtapaRef,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscritaCanonica.EscalaPadrao),
        },
        ArgsElimCorteEmArea corteEmArea => new JsonObject
        {
            ["areaCodigo"] = HashCanonicalComputer.NormalizeNfc(corteEmArea.AreaCodigo),
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteEmArea.Minimo, EscritaCanonica.EscalaPadrao),
        },
        ArgsElimZeroEmArea or ArgsElimFaltaEmDiaDeProvaEnem => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ArgsRegraEliminacao)} não reconhecida: {args.GetType()}."),
    };
}
