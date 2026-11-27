namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As regras do catálogo que um envelope congelado cita: cada referência é o objeto com código e versão, em qualquer bloco.
/// A leitura tipada (<c>EnvelopeCodec</c> e <c>ReferenciasDeRegraDaConfiguracao</c>) não expõe a lista das referências, e uma lista
/// de blocos escrita aqui ficaria defasada quando um bloco novo citar regra; por isso a varredura é pelo formato congelado.
/// </summary>
internal static class RegrasCitadasNosEnvelopesDeReferencia
{
    internal static readonly string[] Arquivos = ["envelope.json", "envelope-rico.json", "envelope-cascata.json"];

    internal static IReadOnlyList<(string Codigo, string Versao)> DoArquivo(string arquivo) =>
        Do(JsonNode.Parse(EnvelopesDeReferenciaDeTeste.Ler(arquivo))!);

    internal static IReadOnlyList<(string Codigo, string Versao)> Do(JsonNode envelope) =>
        [.. Objetos(envelope).Select(static objeto => ComoReferencia(objeto)).OfType<(string, string)>().Distinct()];

    /// <summary>Os objetos que usam a chave <c>versao</c> sem serem referência (código e versão em texto): um uso novo da chave não passa despercebido.</summary>
    internal static int ObjetosComVersaoQueNaoSaoReferencia(JsonNode envelope) =>
        Objetos(envelope).Count(static objeto => objeto.ContainsKey("versao") && ComoReferencia(objeto) is null);

    private static IEnumerable<JsonObject> Objetos(JsonNode? no) => no switch
    {
        JsonObject objeto => [objeto, .. objeto.SelectMany(static propriedade => Objetos(propriedade.Value))],
        JsonArray lista => lista.SelectMany(Objetos),
        _ => [],
    };

    private static (string Codigo, string Versao)? ComoReferencia(JsonObject objeto) =>
        ComoTexto(objeto["codigo"]) is { } codigo && ComoTexto(objeto["versao"]) is { } versao ? (codigo, versao) : null;

    private static string? ComoTexto(JsonNode? valor) =>
        valor is JsonValue texto && texto.TryGetValue(out string? conteudo) ? conteudo : null;
}
