namespace Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>O código congelado vai ao documento; a manifestação não prevista (nula no congelado) deixa a propriedade ausente.</summary>
internal static class ProjecaoDeManifestacaoDeInteresse
{
    public static JsonNode? Projetar(JsonObject congelado, FormaDoNo formaDaParte, ChavesDoDocumentoCongelado chaves) =>
        congelado["manifestacaoDeInteresse"] is JsonValue codigo && codigo.TryGetValue(out string? texto) ? JsonValue.Create(texto) : null;
}
