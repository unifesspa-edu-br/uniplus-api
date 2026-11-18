namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Escrita dos procedimentos de recurso de uma janela no envelope: um objeto por procedimento, na ordem canônica da
/// coleção (tipo de decisão, tipo de alvo, valor), de modo que a mesma declaração dê os mesmos bytes e o mesmo hash.
/// </summary>
internal static class SerializacaoDosProcedimentosDeRecurso
{
    internal const string AlvoItemDeAvaliacao = "item_avaliacao";
    internal const string AlvoAreaDoEnem = "area_enem";

    internal static JsonArray Serializar(ProcedimentosDeRecurso procedimentos) =>
        new([.. procedimentos.Itens.Select(static p => (JsonNode)new JsonObject
        {
            ["tipoDeDecisao"] = p.TipoDeDecisao.ToCodigo(),
            ["alvo"] = SerializarAlvo(p.Alvo),
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(p.Regra),
        })]);

    private static JsonObject? SerializarAlvo(AlvoDoProcedimentoDeRecurso? alvo) => alvo is null
        ? null
        : new JsonObject
        {
            ["tipo"] = alvo.Tipo == TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao ? AlvoItemDeAvaliacao : AlvoAreaDoEnem,
            ["valor"] = alvo.Valor,
        };
}
