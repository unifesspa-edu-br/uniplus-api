namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

/// <summary>Confere a recusa que saiu pelo registro de erros (ADR-0023): status, mídia, code, type na página do catálogo, title, detail e traceId.</summary>
internal static class ProblemaRegistrado
{
    internal static async Task ConferirAsync(
        HttpResponseMessage resposta, HttpStatusCode status, string code, string title, string detail)
    {
        resposta.StatusCode.Should().Be(status);
        resposta.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement raiz = corpo.RootElement;
        raiz.GetProperty("code").GetString().Should().Be(code);
        raiz.GetProperty("type").GetString().Should().Be($"https://unifesspa-edu-br.github.io/uniplus-developers/erros/{code}");
        raiz.GetProperty("title").GetString().Should().Be(title);
        raiz.GetProperty("detail").GetString().Should().Be(detail);
        raiz.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }
}
