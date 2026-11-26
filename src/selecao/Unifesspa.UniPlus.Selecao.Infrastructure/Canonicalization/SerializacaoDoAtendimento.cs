namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Projeta no envelope o atendimento especializado.
/// </summary>
internal static class SerializacaoDoAtendimento
{
    internal static JsonObject SerializarAtendimento(ProcessoSeletivo processo)
    {
        AtendimentoEspecializado atendimento = AtendimentoDefinido(processo);

        return new JsonObject
        {
            ["condicoes"] = SerializarCondicoes(atendimento.Condicoes),
            ["recursos"] = SerializarRecursos(atendimento.Recursos),
            ["tiposDeficiencia"] = SerializarTiposDeDeficiencia(atendimento.TiposDeficiencia),
        };
    }

    private static AtendimentoEspecializado AtendimentoDefinido(ProcessoSeletivo processo) =>
        processo.AtendimentoEspecializado ?? throw new InvalidOperationException(
            "Canonicalização de processo sem atendimento especializado — a verificação de conformidade deveria ter recusado a transição antes deste ponto.");

    private static JsonArray SerializarCondicoes(IEnumerable<CondicaoOfertada> condicoes) =>
        new([.. condicoes
            .OrderBy(static c => c.CondicaoOrigemId)
            .Select(static c => (JsonNode)new JsonObject
            {
                ["condicaoOrigemId"] = c.CondicaoOrigemId,
                ["condicaoCodigo"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoCodigo),
                ["condicaoNome"] = HashCanonicalComputer.NormalizeNfc(c.CondicaoNome),
            })]);

    private static JsonArray SerializarRecursos(IEnumerable<RecursoOfertado> recursos) =>
        new([.. recursos
            .OrderBy(static r => r.RecursoOrigemId)
            .Select(static r => (JsonNode)new JsonObject
            {
                ["recursoOrigemId"] = r.RecursoOrigemId,
                ["recursoCodigo"] = r.RecursoCodigo,
                ["recursoNome"] = HashCanonicalComputer.NormalizeNfc(r.RecursoNome),
            })]);

    private static JsonArray SerializarTiposDeDeficiencia(IEnumerable<DeficienciaAtendida> deficiencias) =>
        new([.. deficiencias
            .OrderBy(static t => t.TipoDeficienciaOrigemId)
            .Select(static t => (JsonNode)new JsonObject
            {
                ["tipoDeficienciaOrigemId"] = t.TipoDeficienciaOrigemId,
                ["tipoDeficienciaCodigo"] = t.TipoDeficienciaCodigo,
                ["tipoDeficienciaNome"] = HashCanonicalComputer.NormalizeNfc(t.TipoDeficienciaNome),
            })]);
}
