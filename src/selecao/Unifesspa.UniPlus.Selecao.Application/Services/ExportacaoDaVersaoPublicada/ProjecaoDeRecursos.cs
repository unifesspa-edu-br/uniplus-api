namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json;
using System.Text.Json.Nodes;

internal static class ProjecaoDeRecursos
{
    public static void Preparar(JsonObject origem, ChavesDoDocumentoCongelado chaves)
    {
        if (origem["regraRecurso"] is JsonObject regra)
        {
            origem["regraRecurso"] = PrepararRecurso(regra, origem, chaves);
        }
        if (origem["recursos"] is JsonArray recursos)
        {
            origem["recursos"] = new JsonArray([.. recursos.OfType<JsonObject>()
                .Select(recurso => (JsonNode)PrepararRecurso(recurso, origem, chaves))]);
        }
    }

    private static JsonObject PrepararRecurso(JsonObject recurso, JsonObject lugar, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject copia = (JsonObject)recurso.DeepClone();
        if (recurso["eventoInicialDoPrazo"] is { } evento)
            copia["eventoInicialDoPrazo"] = JsonNamingPolicy.CamelCase.ConvertName(evento.ToString());
        ProjecaoDeParte.Transferir(copia, "regraCodigo", recurso["regra"]?["codigo"]);
        ProjecaoDeParte.Transferir(copia, "regraVersao", recurso["regra"]?["versao"]);
        if (recurso["parametros"] is JsonObject parametros)
        {
            foreach ((string nome, JsonNode? valor) in parametros)
                ProjecaoDeParte.Transferir(copia, nome, valor);
        }
        AcrescentarReferencia(copia, recurso, lugar);
        if (recurso["procedimentos"] is JsonArray procedimentos)
            copia["procedimentosDeRecurso"] = new JsonArray([.. procedimentos.OfType<JsonObject>().Select(item => PrepararProcedimento(item, chaves))]);
        return copia;
    }

    private static void AcrescentarReferencia(JsonObject copia, JsonObject recurso, JsonObject lugar)
    {
        if (recurso["publicacaoDeReferenciaId"] is not { } identificador)
            return;
        JsonObject? publicacao = (lugar["publicacoesPrevistas"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["id"]?.ToString(), identificador.ToString(), StringComparison.OrdinalIgnoreCase));
        ProjecaoDeParte.Transferir(copia, "publicacaoDeReferenciaAtoCodigo", publicacao?["atoCodigo"]
            ?? throw new InvalidOperationException($"Publicação '{identificador}' não encontrada no documento congelado."));
    }

    private static JsonObject PrepararProcedimento(JsonObject procedimento, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject copia = (JsonObject)procedimento.DeepClone();
        if (procedimento["alvo"] is JsonObject alvo && alvo["valor"] is { } valor)
        {
            copia["alvo"] = alvo["tipo"]?.ToString() switch
            {
                "item_avaliacao" => new JsonObject { ["itemDeAvaliacaoChave"] = chaves.ItemDeAvaliacao(valor.ToString()) },
                "area_enem" => new JsonObject { ["areaDoEnem"] = valor.DeepClone() },
                _ => throw new InvalidOperationException($"Tipo de alvo '{alvo["tipo"]}' não encontrado no documento congelado."),
            };
        }
        return copia;
    }
}
