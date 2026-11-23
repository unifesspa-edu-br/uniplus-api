namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

// A troca usa o vocabulário público; o codec compartilhado conserva a forma gravada no banco.
internal static class PredicadoDaTroca
{
    internal static Result<PredicadoDnf> Ler(JsonElement documento)
    {
        JsonNode? copia = JsonNode.Parse(documento.GetRawText());
        if (copia is JsonArray clausulas)
        {
            foreach (JsonArray clausula in clausulas.OfType<JsonArray>())
            {
                foreach (JsonObject condicao in clausula.OfType<JsonObject>())
                {
                    TraduzirCondicao(condicao);
                }
            }
        }

        return PredicadoDnfJson.DeJson(JsonSerializer.SerializeToElement(copia));
    }

    private static void TraduzirCondicao(JsonObject condicao)
    {
        if (condicao.Remove("codigoDoDado", out JsonNode? codigo))
        {
            condicao["fato"] = codigo;
        }
    }
}
