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
/// Projeta no envelope o atendimento especializado e o bônus regional.
/// </summary>
internal static class SerializacaoDoBonusEDoAtendimento
{
    internal static JsonObject SerializarAtendimento(ProcessoSeletivo processo)
    {
        if (processo.OfertaAtendimento is not { } oferta)
        {
            throw new InvalidOperationException(
                "Canonicalização de processo sem oferta de atendimento especializado — o gate de conformidade deveria ter recusado a transição antes deste ponto.");
        }

        return new JsonObject
        {
            ["condicoes"] = new JsonArray([.. oferta.Condicoes
                .OrderBy(static c => c.CondicaoOrigemId)
                .Select(static c => (JsonNode)new JsonObject
                {
                    ["condicaoOrigemId"] = c.CondicaoOrigemId,
                    ["condicaoCodigo"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoCodigo),
                    ["condicaoNome"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoNome),
                })]),
            ["recursos"] = new JsonArray([.. oferta.Recursos
                .OrderBy(static r => r.RecursoOrigemId)
                .Select(static r => (JsonNode)new JsonObject
                {
                    ["recursoOrigemId"] = r.RecursoOrigemId,
                    ["recursoCodigo"] = r.RecursoCodigo,
                    ["recursoNome"] = HashCanonicalComputer.NormalizeNfc(r.RecursoNome),
                })]),
            ["tiposDeficiencia"] = new JsonArray([.. oferta.TiposDeficiencia
                .OrderBy(static t => t.TipoDeficienciaOrigemId)
                .Select(static t => (JsonNode)new JsonObject
                {
                    ["tipoDeficienciaOrigemId"] = t.TipoDeficienciaOrigemId,
                    ["tipoDeficienciaCodigo"] = t.TipoDeficienciaCodigo,
                    ["tipoDeficienciaNome"] = HashCanonicalComputer.NormalizeNfc(t.TipoDeficienciaNome),
                })]),
        };
    }

    internal static JsonObject SerializarBonusRegional(ProcessoSeletivo processo)
    {
        if (processo.BonusRegional is not { } bonus)
        {
            return new JsonObject { ["presente"] = false };
        }

        return new JsonObject
        {
            ["presente"] = true,
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(bonus.Regra),
            ["fator"] = HashCanonicalComputer.SerializeDecimalCanonical(bonus.Fator, EscritaCanonica.EscalaPadrao),
            ["teto"] = bonus.Teto is { } teto ? HashCanonicalComputer.SerializeDecimalCanonical(teto, EscritaCanonica.EscalaPadrao) : null,
            ["fatoElegibilidadeCodigo"] = bonus.FatoElegibilidadeCodigo,
            ["baseLegalBonusRegionalId"] = bonus.BaseLegalBonusRegionalId,
            ["tipoInstrumento"] = HashCanonicalComputer.NormalizeNfc(bonus.TipoInstrumento),
            ["identificacao"] = HashCanonicalComputer.NormalizeNfc(bonus.Identificacao),
            ["descricao"] = HashCanonicalComputer.NormalizeNfc(bonus.Descricao),
            ["municipios"] = SerializarMunicipiosDoBonus(bonus),
        };
    }

    private static JsonArray SerializarMunicipiosDoBonus(ConfiguracaoBonusRegional bonus) =>
        new([.. bonus.Municipios
            .OrderBy(static m => m.CodigoIbge, StringComparer.Ordinal)
            .ThenBy(static m => HashCanonicalComputer.NormalizeNfc(m.Nome), StringComparer.Ordinal)
            .ThenBy(static m => HashCanonicalComputer.NormalizeNfc(m.Uf), StringComparer.Ordinal)
            .Select(static m => (JsonNode)new JsonObject
            {
                ["codigoIbge"] = HashCanonicalComputer.NormalizeNfc(m.CodigoIbge),
                ["nome"] = HashCanonicalComputer.NormalizeNfc(m.Nome),
                ["uf"] = HashCanonicalComputer.NormalizeNfc(m.Uf),
            })]);
}
