namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O <c>POST /processos-seletivos</c> com o nome omitido ou nulo é recusado com 422 apontando o campo
/// <c>nome</c>, junto das demais recusas do pedido, e não com o 400 do framework.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CriarProcessoSeletivoSemNomeEndpointTests(CascadingFixture fixture)
{
    private const string Rota = "/api/selecao/processos-seletivos";

    [Theory(DisplayName = "O processo sem o nome, omitido ou nulo, é recusado com 422 apontando o nome")]
    [InlineData("{ }")]
    [InlineData("""{ "nome": null }""")]
    public async Task Criar_SemONome_RecusaOCampo(string corpo)
    {
        using HttpResponseMessage resposta = await PostAsync(corpo);

        string conteudo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, conteudo);
        using JsonDocument problema = JsonDocument.Parse(conteudo);
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))
            .Should().Contain(("nome", "uniplus.validacao.obrigatorio"));
    }

    private async Task<HttpResponseMessage> PostAsync(string corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(Rota, UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        using HttpClient client = fixture.Factory.CreateClient();
        return await client.SendAsync(request);
    }
}
