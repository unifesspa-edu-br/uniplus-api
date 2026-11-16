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

    private static readonly JsonSchema Schema = MontarEsquema();

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
        EvaluationResults resultado = Schema.Evaluate(JsonSerializer.SerializeToElement(documento), Opcoes);
        string[] invalidos = [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNot(r)).Select(static r => r.EvaluationPath.ToString())];
        return [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNot(r))
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

    private static JsonSchema MontarEsquema()
    {
        BuildOptions construcao = new() { SchemaRegistry = new SchemaRegistry() };
        Dictionary<string, JsonSchema> construidos = Esquemas.ToDictionary(
            static par => par.Key, par => JsonSchema.FromText(par.Value.ToJsonString(), construcao), StringComparer.Ordinal);
        return construidos[ArquivoDoDocumento];
    }

    private static bool EstaSobNot(EvaluationResults resultado)
    {
        string caminho = resultado.EvaluationPath.ToString();
        return caminho.EndsWith("/not", StringComparison.Ordinal) || caminho.Contains("/not/", StringComparison.Ordinal);
    }

    private static IEnumerable<EvaluationResults> Detalhes(EvaluationResults resultado) =>
        (resultado.Details ?? []).SelectMany(Detalhes).Prepend(resultado);
}
