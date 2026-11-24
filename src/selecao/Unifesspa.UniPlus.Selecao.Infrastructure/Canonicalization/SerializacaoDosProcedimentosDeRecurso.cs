namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Escrita dos procedimentos de recurso de uma janela no envelope: um objeto por procedimento, na ordem canônica da
/// coleção (tipo de decisão, tipo de alvo, valor), de modo que a mesma declaração dê os mesmos bytes e o mesmo hash.
/// </summary>
internal static class SerializacaoDosProcedimentosDeRecurso
{
    internal static JsonArray Serializar(ProcedimentosDeRecurso procedimentos) =>
        new([.. procedimentos.Itens.Select(static p => (JsonNode)SerializarProcedimento(p))]);

    private static JsonObject SerializarProcedimento(ProcedimentoDeRecursoDeclarado procedimento)
    {
        JsonObject objeto = new()
        {
            ["tipoDeDecisao"] = procedimento.TipoDeDecisao.Codigo(),
            ["alvo"] = SerializarAlvo(procedimento.Alvo),
            ["regra"] = EscritaCanonica.SerializarReferenciaRegra(procedimento.Regra),
        };
        ContextoDosRotulos.Acrescentar(objeto, "tipoDeDecisao", procedimento.TipoDeDecisao);
        return objeto;
    }

    private static JsonObject? SerializarAlvo(AlvoDoProcedimentoDeRecurso? alvo)
    {
        if (alvo is null)
        {
            return null;
        }
        JsonObject objeto = new() { ["tipo"] = alvo.Tipo.Codigo(), ["valor"] = alvo.Valor };
        ContextoDosRotulos.Acrescentar(objeto, "tipo", alvo.Tipo);
        return objeto;
    }
}
