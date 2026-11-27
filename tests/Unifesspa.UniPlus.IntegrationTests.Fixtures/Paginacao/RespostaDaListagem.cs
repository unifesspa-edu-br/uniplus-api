namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// Confere o status de uma resposta e, quando é outro, mostra na falha o código, o detalhe e o corpo inteiros do problema,
/// sem que as chaves do JSON sejam lidas como parte do texto da mensagem.
/// </summary>
public static class RespostaDaListagem
{
    /// <summary>Confere o status e devolve o corpo lido.</summary>
    public static async Task<string> ConferirStatusAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string corpo = await resposta.Content.ReadAsStringAsync().ConfigureAwait(false);
        resposta.StatusCode.Should().Be(esperado, "a resposta foi: {0}", Descrever(corpo));

        return corpo;
    }

    private static string Descrever(string corpo)
    {
        try
        {
            using JsonDocument problema = JsonDocument.Parse(corpo);
            string codigo = Propriedade(problema.RootElement, "code");
            string detalhe = Propriedade(problema.RootElement, "detail");

            return $"código '{codigo}', detalhe '{detalhe}', corpo inteiro: {corpo}";
        }
        catch (JsonException)
        {
            return $"corpo que não é JSON: {corpo}";
        }
    }

    private static string Propriedade(JsonElement raiz, string nome) =>
        raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty(nome, out JsonElement valor) ? valor.ToString() : "(ausente)";
}
