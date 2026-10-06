namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O corpus de casos de <c>contracts/formularios/casos</c>, que a API e o interpretador do front rodam
/// (ADR-0139): cada caso é o corpo da avaliação sem cadastro — regras e respostas — mais o resultado
/// esperado, que tem de estar contido no que o avaliador devolve. Um caso que diverge aqui é regra que o
/// front interpretaria diferente do servidor.
/// </summary>
public sealed class CorpusDeAvaliacaoTests
{
    private static readonly JsonSerializerOptions Fio = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> Casos() =>
        new(Directory.GetFiles(PastaDosCasos(), "*.json").Select(static c => Path.GetFileName(c)).Order(StringComparer.Ordinal));

    [Theory(DisplayName = "O avaliador devolve o resultado esperado de cada caso do corpus")]
    [MemberData(nameof(Casos))]
    public void Avaliar_CasoDoCorpus_ContemOEsperado(string arquivo)
    {
        string conteudo = File.ReadAllText(Path.Join(PastaDosCasos(), arquivo));
        AvaliacaoSemCadastroInput simulacao = JsonSerializer.Deserialize<AvaliacaoSemCadastroInput>(conteudo, Fio)!;
        JsonNode esperado = JsonNode.Parse(conteudo)!["esperado"]!;

        Result<AvaliacaoPortavel> resultado = AvaliarFormularioSemCadastroQueryHandler.Handle(new AvaliarFormularioSemCadastroQuery(simulacao));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        List<string> divergencias = [];
        Conferir(esperado, JsonSerializer.SerializeToNode(resultado.Value, Fio), "esperado", divergencias);
        divergencias.Should().BeEmpty();
    }

    /// <summary>
    /// O esperado contido no resultado: cada propriedade do objeto esperado confere com a do resultado;
    /// cada objeto de uma lista esperada confere com algum objeto da lista do resultado; e o valor simples
    /// — inclusive a lista de valores simples — é igual.
    /// </summary>
    private static void Conferir(JsonNode? esperado, JsonNode? real, string caminho, List<string> divergencias)
    {
        switch (esperado)
        {
            case JsonObject objeto when real is JsonObject doResultado:
                foreach ((string chave, JsonNode? valor) in objeto)
                {
                    Conferir(valor, doResultado[chave], $"{caminho}.{chave}", divergencias);
                }

                break;

            case JsonArray lista when lista.Any(static e => e is JsonObject) && real is JsonArray doResultado:
                foreach ((JsonNode? elemento, int indice) in lista.Select(static (e, i) => (e, i)))
                {
                    if (!doResultado.Any(r => Contem(elemento, r)))
                    {
                        divergencias.Add($"{caminho}[{indice}]: nenhum elemento do resultado contém {elemento?.ToJsonString()}");
                    }
                }

                break;

            default:
                if (!JsonNode.DeepEquals(esperado, real))
                {
                    divergencias.Add($"{caminho}: esperado {esperado?.ToJsonString() ?? "null"}, veio {real?.ToJsonString() ?? "null"}");
                }

                break;
        }
    }

    private static bool Contem(JsonNode? esperado, JsonNode? real)
    {
        List<string> divergencias = [];
        Conferir(esperado, real, string.Empty, divergencias);
        return divergencias.Count == 0;
    }

    private static string PastaDosCasos([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(Path.GetDirectoryName(origem)!, "..", "..", "..", "contracts", "formularios", "casos"));
}
