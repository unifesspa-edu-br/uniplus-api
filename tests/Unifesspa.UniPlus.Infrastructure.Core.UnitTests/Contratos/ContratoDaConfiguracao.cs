namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Contratos;

using System.Text.Json;
using System.Text.Json.Nodes;

using Json.Schema;

/// <summary>
/// Acesso comum dos testes de contrato ao esquema da configuração do processo seletivo e aos exemplos publicados:
/// carrega os arquivos copiados para a saída do teste e devolve os caminhos das propriedades que o documento viola.
/// </summary>
internal static class ContratoDaConfiguracao
{
    internal static readonly string PastaDoContrato = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao");

    internal static readonly string[] ArquivosDeExemplo = [.. Directory.GetFiles(Path.Join(PastaDoContrato, "exemplos"), "*.json")
        .Select(static caminho => Path.GetFileName(caminho))];

    private static readonly JsonSchema Schema = JsonSchema.FromText(File.ReadAllText(Path.Join(PastaDoContrato, "v1.schema.json")));

    private static readonly EvaluationOptions Opcoes = new() { RequireFormatValidation = true, OutputFormat = OutputFormat.List };

    internal static JsonNode Documento(string json) => JsonNode.Parse(json)!;

    internal static JsonNode Carregar(string relativo) => JsonNode.Parse(File.ReadAllText(Path.Join(PastaDoContrato, relativo)))!;

    internal static JsonNode EsquemaComoNo() => JsonNode.Parse(File.ReadAllText(Path.Join(PastaDoContrato, "v1.schema.json")))!;

    internal static string[] CaminhosComErro(JsonNode documento)
    {
        EvaluationResults resultado = Schema.Evaluate(JsonSerializer.SerializeToElement(documento), Opcoes);
        string[] invalidos = [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNot(r)).Select(static r => r.EvaluationPath.ToString())];
        return [.. Detalhes(resultado).Where(static r => !r.IsValid && !EstaSobNot(r))
            .Where(r => !invalidos.Any(outro => outro.StartsWith(r.EvaluationPath + "/", StringComparison.Ordinal)))
            .Select(static r => r.InstanceLocation.ToString()).Distinct()];
    }

    private static bool EstaSobNot(EvaluationResults resultado)
    {
        string caminho = resultado.EvaluationPath.ToString();
        return caminho.EndsWith("/not", StringComparison.Ordinal) || caminho.Contains("/not/", StringComparison.Ordinal);
    }

    private static IEnumerable<EvaluationResults> Detalhes(EvaluationResults resultado) =>
        (resultado.Details ?? []).SelectMany(Detalhes).Prepend(resultado);

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
}
