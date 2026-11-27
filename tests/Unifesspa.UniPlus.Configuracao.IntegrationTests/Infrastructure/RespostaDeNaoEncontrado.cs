namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>
/// Confere a recusa de consulta por Id inexistente: 404 em <c>application/problem+json</c>, com o
/// código de erro do cadastro e o <c>type</c> apontando para a página desse código no catálogo.
/// </summary>
internal static class RespostaDeNaoEncontrado
{
    internal static async Task ConferirAsync(HttpResponseMessage resposta, string codigoEsperado)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be(codigoEsperado);
        problema.RootElement.GetProperty("type").GetString().Should().EndWith($"/erros/{codigoEsperado}");
    }
}
