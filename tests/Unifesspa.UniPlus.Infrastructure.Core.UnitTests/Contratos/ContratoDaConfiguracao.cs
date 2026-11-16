namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Json.Schema;

/// <summary>
/// Acesso comum dos testes de contrato ao esquema da configuração do processo seletivo, dividido em arquivos que se
/// referenciam entre si, e aos exemplos publicados: carrega os arquivos copiados para a saída do teste e devolve os caminhos
/// das propriedades que o documento viola.
/// </summary>
internal static class ContratoDaConfiguracao
{
    internal const string ArquivoDoDocumento = "documento.schema.json";

    internal static readonly string PastaDoContrato = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao");

    internal static readonly string[] ArquivosDeExemplo = [.. Directory.GetFiles(Path.Join(PastaDoContrato, "exemplos"), "*.json")
        .Select(static caminho => Path.GetFileName(caminho))];

    private static readonly Dictionary<string, JsonNode> Esquemas = Directory.GetFiles(Path.Join(PastaDoContrato, "v1"), "*.schema.json")
        .ToDictionary(static caminho => Path.GetFileName(caminho), static caminho => JsonNode.Parse(File.ReadAllText(caminho))!, StringComparer.Ordinal);

    // O registro de esquemas da biblioteca não aceita uso concorrente, e as classes de teste rodam em paralelo.
    private static readonly Lock Trava = new();

    private static readonly BuildOptions Construcao = new() { SchemaRegistry = new SchemaRegistry() };

    private static readonly Dictionary<string, JsonSchema> Construidos = Esquemas.ToDictionary(
        static par => par.Key, static par => JsonSchema.FromText(par.Value.ToJsonString(), Construcao), StringComparer.Ordinal);

    private static readonly JsonSchema Schema = Construidos[ArquivoDoDocumento];

    private static readonly EvaluationOptions Opcoes = new() { RequireFormatValidation = true, OutputFormat = OutputFormat.List };

    internal static IEnumerable<string> ArquivosDoEsquema => Esquemas.Keys;

    internal static JsonNode Documento(string json) => JsonNode.Parse(json)!;

    internal static JsonNode Carregar(string relativo) => JsonNode.Parse(File.ReadAllText(Path.Join(PastaDoContrato, relativo)))!;

    internal static JsonNode Esquema(string arquivo) => Esquemas[arquivo];

    /// <summary>Resolve uma referência <c>$ref</c> relativa, dentro do arquivo ou para outro arquivo do esquema.</summary>
    internal static JsonNode ResolverReferencia(string arquivoDeOrigem, string referencia)
    {
        string[] partes = referencia.Split('#', 2);
        JsonNode alvo = Esquema(partes[0].Length == 0 ? arquivoDeOrigem : partes[0]);
        string[] passos = partes.Length == 2 ? partes[1].Split('/', StringSplitOptions.RemoveEmptyEntries) : [];
        return passos.Aggregate(alvo, static (no, passo) => no[passo]!);
    }

    internal static string[] CaminhosComErro(JsonNode documento)
    {
        lock (Trava)
        {
            return CaminhosComErro(Schema, documento);
        }
    }

    /// <summary>Os caminhos que a instância viola contra uma definição do esquema, citada como <c>arquivo#/$defs/nome</c>.</summary>
    internal static string[] CaminhosComErro(JsonNode instancia, string definicao)
    {
        string[] partes = definicao.Split('#', 2);
        string id = Esquemas[partes[0]]["$id"]!.GetValue<string>();
        lock (Trava)
        {
            JsonSchema alvo = JsonSchema.FromText($$"""{ "$ref": "{{id}}{{(partes.Length == 2 ? "#" + partes[1] : string.Empty)}}" }""", Construcao);
            return CaminhosComErro(alvo, instancia);
        }
    }

    private static string[] CaminhosComErro(JsonSchema schema, JsonNode documento)
    {
        EvaluationResults resultado = schema.Evaluate(JsonSerializer.SerializeToElement(documento), Opcoes);
        if (resultado.IsValid)
        {
            return [];
        }

        string[] invalidos = [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNegacaoOuCondicao(r)).Select(static r => r.EvaluationPath.ToString())];
        return [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNegacaoOuCondicao(r))
            .Where(r => !invalidos.Any(outro => outro.StartsWith(r.EvaluationPath + "/", StringComparison.Ordinal)))
            .Select(static r => r.InstanceLocation.ToString()).Distinct()];
    }

    internal static void Atribuir(JsonNode raiz, string ponteiro, JsonNode valor)
    {
        string[] passos = ponteiro.Split('/', StringSplitOptions.RemoveEmptyEntries);
        JsonNode atual = raiz;
        foreach (string passo in passos[..^1])
        {
            atual = atual is JsonArray lista ? lista[int.Parse(passo, System.Globalization.CultureInfo.InvariantCulture)]! : atual[passo]!;
        }

        atual[passos[^1]] = valor;
    }

    // O que falha sob "not" ou sob a condição "if" não é violação do documento: é a avaliação que escolhe o ramo.
    private static bool EstaSobNegacaoOuCondicao(EvaluationResults resultado)
    {
        string caminho = resultado.EvaluationPath.ToString();
        return new[] { "/not", "/if" }.Any(ramo => caminho.EndsWith(ramo, StringComparison.Ordinal) || caminho.Contains(ramo + "/", StringComparison.Ordinal));
    }

    private static IEnumerable<EvaluationResults> Detalhes(EvaluationResults resultado) =>
        (resultado.Details ?? []).SelectMany(Detalhes).Prepend(resultado);
}
