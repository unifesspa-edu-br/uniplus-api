namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

internal static class ContextoDosRotulos
{
    internal static void Acrescentar<TEnum>(JsonObject objeto, string campo, TEnum valor)
        where TEnum : struct, Enum =>
        Acrescentar(objeto, campo, (TEnum?)valor);

    internal static void Acrescentar<TEnum>(JsonObject objeto, string campo, TEnum? valor)
        where TEnum : struct, Enum
    {
        if (valor is null)
        {
            return;
        }

        JsonObject contexto = objeto["contexto"] as JsonObject ?? CriarContexto(objeto);
        contexto[$"{campo}Rotulo"] = valor.Value.Rotulo();
    }

    /// <summary>Rótulos de uma lista de códigos, na mesma ordem e do mesmo tamanho da lista gravada ao lado.</summary>
    internal static void AcrescentarLista<TEnum>(JsonObject objeto, string campo, IEnumerable<TEnum> valores)
        where TEnum : struct, Enum
    {
        JsonObject contexto = objeto["contexto"] as JsonObject ?? CriarContexto(objeto);
        contexto[$"{campo}Rotulos"] = new JsonArray([.. valores.Select(static valor => (JsonNode)valor.Rotulo())]);
    }

    /// <summary>Remove o contexto de todos os níveis: o rótulo é registro de leitura, nunca critério de comparação.</summary>
    internal static JsonNode SemContexto(JsonNode no)
    {
        JsonNode copia = no.DeepClone();
        RemoverContexto(copia);
        return copia;
    }

    private static void RemoverContexto(JsonNode? no)
    {
        if (no is JsonObject objeto)
        {
            objeto.Remove("contexto");
            objeto.Select(static par => par.Value).ToList().ForEach(RemoverContexto);
        }
        else if (no is JsonArray lista)
        {
            lista.ToList().ForEach(RemoverContexto);
        }
    }

    private static JsonObject CriarContexto(JsonObject objeto)
    {
        JsonObject contexto = [];
        objeto["contexto"] = contexto;
        return contexto;
    }
}
