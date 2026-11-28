namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DefinirProvaPresencialEndpointTests(CascadingFixture fixture)
{
    [Theory]
    [InlineData("150420", "PA", "uniplus.cidade_referencia.codigo_ibge_formato_invalido")]
    [InlineData("1504208", "SP", "uniplus.cidade_referencia.uf_incoerente")]
    public async Task Put_MunicipioInvalido_RetornaCodigoDoDominio(string codigo, string uf, string erro)
    {
        Guid id = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = CriarRequest(id, codigo, uf);
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("code").GetString())
            .Should().Contain(erro);
    }

    [Fact]
    public async Task Put_ItemNulo_RetornaRecusasDoDominio()
    {
        Guid id = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = CriarRequest(id, "1504208", "PA");
        request.Content = new StringContent("{ \"temProvaPresencial\": true, \"municipios\": [null] }", Encoding.UTF8, "application/json");
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("code").GetString())
            .Should().Contain("uniplus.cidade_referencia.codigo_ibge_obrigatorio");
    }

    [Theory(DisplayName = "Corpo sem a declaração ou sem a lista de municípios é recusado com 422 de campo obrigatório")]
    [InlineData("{ }", new[] { "provaPresencial.temProvaPresencial", "provaPresencial.municipios" })]
    [InlineData("""{ "temProvaPresencial": true, "municipios": null }""", new[] { "provaPresencial.municipios" })]
    [InlineData("""{ "municipios": [] }""", new[] { "provaPresencial.temProvaPresencial" })]
    public async Task Put_CampoAusente_RecusaComoObrigatorio(string corpo, string[] campos)
    {
        Guid id = await SemearAsync();
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = CriarRequest(id, "1504208", "PA");
        request.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
        using HttpResponseMessage resposta = await client.SendAsync(request);
        await RecusaDeObrigatorioDoDominio.ConferirAsync(resposta, campos);
    }

    [Fact]
    public async Task Put_ProcessoInexistente_Retorna404()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = CriarRequest(Guid.NewGuid(), "1504208", "PA");
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SemearAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        OpcoesDoProcessoSeletivoPublicavel opcoes = new() { Nome = $"Prova presencial {Guid.CreateVersion7()}" };
        (Unifesspa.UniPlus.Selecao.Domain.Entities.ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        return processo.Id;
    }

    private static HttpRequestMessage CriarRequest(Guid id, string codigo, string uf)
    {
        HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{id}/prova-presencial", UriKind.Relative))
        {
            Content = new StringContent($$"""{ "temProvaPresencial": true, "municipios": [{ "codigoIbge": "{{codigo}}", "uf": "{{uf}}" }] }""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return request;
    }
}
