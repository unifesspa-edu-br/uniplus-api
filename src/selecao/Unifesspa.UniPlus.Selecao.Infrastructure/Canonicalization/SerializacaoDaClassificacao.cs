namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoCongelado;
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
        ParametrosDesempateMaiorNotaEtapa maiorNotaEtapa => new JsonObject { ["etapaId"] = maiorNotaEtapa.EtapaId },
        ParametrosDesempateMaiorIdade => [],
        ParametrosDesempateIdoso idoso => new JsonObject { ["idadeMinima"] = idoso.IdadeMinima },
        ParametrosDesempatePredicadoFato predicadoFato => SerializarPredicadoDoDesempate(predicadoFato.Condicao),
        ParametrosDesempateMaiorNotaAreaEnem maiorNotaAreaEnem => SerializarAreasDoDesempate(maiorNotaAreaEnem.Areas),
        _ => throw new InvalidOperationException($"Variante de {nameof(ParametrosCriterioDesempate)} não reconhecida: {parametros.GetType()}."),
    };

    private static JsonObject SerializarPredicadoDoDesempate(CondicaoDnf condicao)
    {
        JsonObject objeto = new()
        {
            ["codigoDoDado"] = HashCanonicalComputer.NormalizeNfc(condicao.Fato),
            ["operador"] = condicao.Operador.Codigo(),
            ["valor"] = SerializacaoDosPredicados.SerializarValorDeAtomo(condicao.Operador, condicao.Valor)
        };
        ContextoDosRotulos.Acrescentar(objeto, "operador", condicao.Operador);
        return objeto;
    }

    private static JsonObject SerializarAreasDoDesempate(IEnumerable<string> areas) => new()
    {
        ["areas"] = new JsonArray([.. areas.Select(static area => (JsonNode?)JsonValue.Create(HashCanonicalComputer.NormalizeNfc(area)))]),
    };

    internal static JsonObject SerializarClassificacao(ProcessoSeletivo processo)
    {
        ConfiguracaoClassificacao classificacao = ClassificacaoDefinida(processo);

        return new JsonObject
        {
            ["regraCalculo"] = EscritaCanonica.SerializarReferenciaRegra(classificacao.RegraCalculo),
            ["regraArredondamento"] = SerializarRegraDeArredondamento(classificacao.RegraArredondamento),
            ["casasArredondamento"] = classificacao.CasasArredondamento,
            ["escalaNotaFinal"] = SerializarEscalaDaNotaFinal(classificacao.EscalaNotaFinal),
            ["regraOrdemAlocacao"] = EscritaCanonica.SerializarReferenciaRegra(classificacao.RegraOrdemAlocacao),
            ["baseadoEmEnem"] = classificacao.BaseadoEmEnem,
            ["regrasEliminacao"] = SerializarRegrasDeEliminacao(classificacao.RegrasEliminacao),
            ["resolucaoPesoAreaEnem"] = SerializarResolucaoDosPesos(classificacao.ResolucaoPesoAreaEnem),
            ["quadroPesoAreaEnem"] = AreasDoEnemNoDocumentoCongelado.QuadroDePesos(classificacao.QuadroPesoAreaEnem),
        };
    }

    private static ConfiguracaoClassificacao ClassificacaoDefinida(ProcessoSeletivo processo) =>
        processo.Classificacao ?? throw new InvalidOperationException(
            "Canonicalização de processo sem configuração de classificação — a verificação de conformidade deveria ter recusado a transição antes deste ponto.");

    private static JsonObject? SerializarRegraDeArredondamento(ReferenciaRegra? arredondamento) =>
        arredondamento is { } regra ? EscritaCanonica.SerializarReferenciaRegra(regra) : null;

    private static string? SerializarEscalaDaNotaFinal(decimal? escalaNotaFinal) =>
        escalaNotaFinal is { } escala
            ? HashCanonicalComputer.SerializeDecimalCanonical(escala, ConfiguracaoClassificacao.EscalaNotaFinalEscala)
            : null;

    private static JsonArray SerializarRegrasDeEliminacao(IEnumerable<RegraEliminacao> regras) =>
        EscritaCanonica.OrdenarPorConteudo(regras
            .Select(static r => new JsonObject
            {
                ["regra"] = EscritaCanonica.SerializarReferenciaRegra(r.Regra),
                ["parametros"] = SerializarParametrosRegraEliminacao(r.Parametros),
            }));

    private static string? SerializarResolucaoDosPesos(string? resolucao) =>
        resolucao is null ? null : HashCanonicalComputer.NormalizeNfc(resolucao);

    private static JsonObject SerializarParametrosRegraEliminacao(ParametrosRegraEliminacao parametros) => parametros switch
    {
        ParametrosEliminacaoPorAusenciaNaEtapa ausencia => new JsonObject { ["etapaId"] = ausencia.EtapaId },
        ParametrosEliminacaoPorNotaMinimaNaEtapa notaMinima => new JsonObject
        {
            ["etapaId"] = notaMinima.EtapaId,
            ["notaMinima"] = HashCanonicalComputer.SerializeDecimalCanonical(notaMinima.NotaMinima, EscritaCanonica.EscalaPadrao),
        },
        ParametrosEliminacaoPorCorteEmAreaDoEnem corteEmArea => new JsonObject
        {
            ["areaCodigo"] = HashCanonicalComputer.NormalizeNfc(corteEmArea.AreaCodigo),
            ["minimo"] = HashCanonicalComputer.SerializeDecimalCanonical(corteEmArea.Minimo, EscritaCanonica.EscalaPadrao),
        },
        ParametrosEliminacaoPorZeroEmAreaDoEnem or ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem => [],
        _ => throw new InvalidOperationException($"Variante de {nameof(ParametrosRegraEliminacao)} não reconhecida: {parametros.GetType()}."),
    };
}
