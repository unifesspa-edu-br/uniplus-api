namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

internal sealed class ChavesDoDocumentoCongelado(JsonObject congelado)
{
    public JsonObject Oferta(string identificador)
    {
        JsonObject? oferta = (congelado["ofertas"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["ofertaCursoOrigemId"]?.ToString(), identificador, StringComparison.OrdinalIgnoreCase));
        return oferta ?? throw new InvalidOperationException($"Oferta de curso '{identificador}' não encontrada no documento congelado.");
    }

    public string CodigoDaOferta(string identificador) => Oferta(identificador)["ofertaCursoCodigo"]?.GetValue<string>()
        ?? throw new InvalidOperationException($"Oferta de curso '{identificador}' sem código no documento congelado.");

    public string Fase(string identificador) => Resolver(Fases(), "id", identificador, "fase");
    public string FasePeloCodigo(string codigo) => Resolver(Fases(), "codigo", codigo, "fase");
    public string FasePeloTipo(string identificador)
    {
        JsonObject[] fases = Fases().Where(fase => string.Equals(fase["tipoFaseOrigemId"]?.ToString(), identificador, StringComparison.OrdinalIgnoreCase)).ToArray();
        return fases.Length == 1 && fases[0]["chave"] is JsonNode chave ? chave.GetValue<string>()
            : throw new InvalidOperationException($"Identificador '{identificador}' de tipo de fase não resolve uma única chave no documento congelado.");
    }
    public string Etapa(string identificador) => Resolver(Etapas(), "id", identificador, "etapa");
    public string ItemDeAvaliacao(string identificador) => Resolver(
        Etapas().SelectMany(etapa => Objetos(etapa["itensDeAvaliacao"])), "id", identificador, "item de avaliação");
    public string Exigencia(string identificador) => Resolver(
        Objetos(congelado["documentosExigidos"]?["exigencias"]), "exigenciaId", identificador, "exigência");

    private IEnumerable<JsonObject> Fases() => Objetos(congelado["cronogramaFases"]?["fases"]);
    private IEnumerable<JsonObject> Etapas() => Objetos(congelado["etapas"]);
    private static IEnumerable<JsonObject> Objetos(JsonNode? valor) => (valor as JsonArray ?? []).OfType<JsonObject>();

    private static string Resolver(IEnumerable<JsonObject> objetos, string campo, string identificador, string tipo)
    {
        JsonObject? objeto = objetos.FirstOrDefault(item => string.Equals(item[campo]?.ToString(), identificador, StringComparison.OrdinalIgnoreCase));
        return objeto?["chave"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Identificador '{identificador}' de {tipo} não resolve uma chave no documento congelado.");
    }
}
