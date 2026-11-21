namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

/// <summary>
/// A leitura que os testes de contrato dos vocabulários repetem: pede o vocabulário sem negociar o tipo de
/// mídia, confere que a resposta é 200 no tipo de mídia da versão 1 do recurso e devolve os itens publicados.
/// </summary>
internal static class ApoioDosVocabularios
{
    internal static async Task<T[]> LerAsync<T>(HttpClient client, string vocabulario, string recurso)
    {
        Uri rota = new($"/api/configuracao/vocabularios/{vocabulario}", UriKind.Relative);

        HttpResponseMessage response = await client.GetAsync(rota);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be($"application/vnd.uniplus.{recurso}.v1+json");
        return (await response.Content.ReadFromJsonAsync<T[]>())!;
    }
}
