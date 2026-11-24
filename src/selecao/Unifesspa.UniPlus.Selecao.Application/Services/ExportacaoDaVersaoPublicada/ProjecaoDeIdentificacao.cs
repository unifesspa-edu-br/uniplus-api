namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

internal static class ProjecaoDeIdentificacao
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves)
    {
        JsonObject origem = [];
        AcrescentarDadosDoProcesso(origem, congelado);
        if (congelado["algoritmoContagemPrazo"] is JsonObject algoritmo && algoritmo["presente"]?.GetValue<bool>() == true)
        {
            origem["algoritmoContagemPrazo"] = algoritmo.DeepClone();
        }
        return origem.Count == 0 ? null : CopiaPeloEsquema.Copiar(origem, formaDaParte);
    }

    private static void AcrescentarDadosDoProcesso(JsonObject origem, JsonObject congelado)
    {
        ProjecaoDeParte.Transferir(origem, "nome", congelado["nome"]);
        Mapear(origem, congelado["tipoProcesso"] as JsonObject, "tipoProcesso", "codigo", "nome");
        Mapear(origem, congelado["identidadesUnidade"]?["administradora"] as JsonObject, "unidadeAdministradora", "codigo", "nome", "sigla");
        Mapear(origem, congelado["localidade"] as JsonObject, "localidade", "codigoIbge", "nome", "uf");
        ProjecaoDeParte.Transferir(origem, "identificadorLegivel", congelado["identificadorLegivel"]);
        ProjecaoDeParte.Transferir(origem, "origemCandidatos", congelado["cronogramaFases"]?["origemCandidatos"]);
    }

    private static void Mapear(JsonObject destino, JsonObject? origem, string prefixo, params string[] campos)
    {
        foreach (string campo in campos)
        {
            ProjecaoDeParte.Transferir(destino, prefixo + char.ToUpperInvariant(campo[0]) + campo[1..], origem?[campo]);
        }
    }
}
