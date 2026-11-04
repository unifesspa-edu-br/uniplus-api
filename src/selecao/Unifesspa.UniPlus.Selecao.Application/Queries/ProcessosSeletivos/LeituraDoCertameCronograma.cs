namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leitura, no envelope congelado, das etapas e do cronograma de fases.
/// </summary>
internal static class LeituraDoCertameCronograma
{
    internal static bool TentarEtapas(JsonObject envelope, [NotNullWhen(true)] out List<EtapaCertameDto>? etapas)
    {
        etapas = null;
        return PrimitivasDaProjecao.TentarArray(envelope, ProjecaoDoCertamePublicado.BlocoPublico("etapas"), out JsonArray? array)
            && PrimitivasDaProjecao.TentarItens<EtapaCertameDto>(array, TentarEtapa, out etapas);
    }

    private static bool TentarEtapa(JsonNode? item, [NotNullWhen(true)] out EtapaCertameDto? etapaLida)
    {
        etapaLida = null;
        if (item is not JsonObject etapa
            || !PrimitivasDaProjecao.TentarTexto(etapa, "nome", out string nome)
            || !PrimitivasDaProjecao.TentarTexto(etapa, "carater", out string carater)
            || !PrimitivasDaProjecao.TentarObjeto(etapa, "tipoEtapa", out JsonObject? tipoEtapaNode)
            || !LeituraDoCertameEdital.TentarTipoNomeado(tipoEtapaNode, out TipoCatalogadoCertameDto? tipoEtapa)
            || !PrimitivasDaProjecao.TentarTextoOpcional(etapa, "peso", out string? peso)
            || !PrimitivasDaProjecao.TentarTextoOpcional(etapa, "notaMinima", out string? notaMinima)
            || !PrimitivasDaProjecao.TentarInteiroOpcional(etapa, "ordem", out int? ordem)
            || !PrimitivasDaProjecao.TentarTextoOpcional(etapa, "faseCodigo", out string? faseCodigo)
            || !PrimitivasDaProjecao.TentarInstanteOpcional(etapa, "inicio", out DateTimeOffset? inicio)
            || !PrimitivasDaProjecao.TentarInstanteOpcional(etapa, "fim", out DateTimeOffset? fim)
            || !PrimitivasDaProjecao.TentarBooleano(etapa, "emiteParecerIndividual", out bool emiteParecer))
        {
            return false;
        }

        etapaLida = new EtapaCertameDto(nome, carater, tipoEtapa, peso, notaMinima, ordem, faseCodigo, inicio, fim, emiteParecer);
        return true;
    }

    internal static bool TentarCronograma(
        JsonObject envelope,
        [NotNullWhen(true)] out string? origemCandidatos,
        [NotNullWhen(true)] out List<FaseCronogramaCertameDto>? fases)
    {
        origemCandidatos = null;
        fases = null;

        if (!PrimitivasDaProjecao.TentarObjeto(envelope, ProjecaoDoCertamePublicado.BlocoPublico("cronogramaFases"), out JsonObject? bloco)
            || !PrimitivasDaProjecao.TentarTexto(bloco, "origemCandidatos", out string origem)
            || !PrimitivasDaProjecao.TentarArray(bloco, "fases", out JsonArray? array)
            || !PrimitivasDaProjecao.TentarItens<FaseCronogramaCertameDto>(array, TentarFase, out List<FaseCronogramaCertameDto>? lidas))
        {
            return false;
        }

        origemCandidatos = origem;
        fases = lidas;
        return true;
    }

    private static bool TentarFase(JsonNode? item, [NotNullWhen(true)] out FaseCronogramaCertameDto? faseLida)
    {
        faseLida = null;
        if (item is not JsonObject fase
            || !PrimitivasDaProjecao.TentarInteiro(fase, "ordem", out int ordem)
            || !PrimitivasDaProjecao.TentarTexto(fase, "codigo", out string codigo)
            || !PrimitivasDaProjecao.TentarInstanteOpcional(fase, "inicio", out DateTimeOffset? inicio)
            || !PrimitivasDaProjecao.TentarInstanteOpcional(fase, "fim", out DateTimeOffset? fim)
            || !PrimitivasDaProjecao.TentarBooleano(fase, "coletaInscricao", out bool coletaInscricao)
            || !PrimitivasDaProjecao.TentarBooleano(fase, "coletaSolicitacaoIsencao", out bool coletaIsencao)
            || !PrimitivasDaProjecao.TentarBooleano(fase, "permiteComplementacao", out bool permiteComplementacao))
        {
            return false;
        }

        faseLida = new FaseCronogramaCertameDto(ordem, codigo, inicio, fim, coletaInscricao, coletaIsencao, permiteComplementacao);
        return true;
    }
}
