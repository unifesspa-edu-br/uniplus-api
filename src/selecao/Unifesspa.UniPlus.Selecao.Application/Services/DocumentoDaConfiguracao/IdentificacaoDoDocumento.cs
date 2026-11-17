namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

/// <summary>Lê do documento já avaliado o que a aplicação precisa saber dele sem abrir o conteúdo: o nome e a unidade.</summary>
internal static class IdentificacaoDoDocumento
{
    public static DocumentoLido Ler(JsonObject documento) => new(
        documento,
        TextoEm(documento, "identificacao", "nome"),
        TextoEm(documento, "identificacao", "unidadeAdministradoraCodigo"));

    private static string? TextoEm(JsonObject documento, string bloco, string propriedade) =>
        documento[bloco] is JsonObject objeto && objeto[propriedade] is JsonValue valor && valor.TryGetValue(out string? texto) ? texto : null;
}
