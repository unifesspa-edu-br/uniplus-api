namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Text.Json;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class FormaAceiteOpenApiTests(CascadingFixture fixture)
{
    [Fact(DisplayName = "O formulário e a tela de termos publicam a forma de aceite")]
    public async Task OpenApi_FormaAceite_PublicaCodigoERotulo()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using JsonDocument spec = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi/selecao.json", UriKind.Relative)));
        JsonElement schemas = spec.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement formulario = schemas.GetProperty("ConteudoDoTermoDto").GetProperty("properties").GetProperty("formaAceite");
        JsonElement tela = schemas.GetProperty("ConteudoDoTermoExigidoDto").GetProperty("properties").GetProperty("formaAceite");

        formulario.GetProperty("$ref").GetString().Should().EndWith("/FormaAceite");
        tela.GetProperty("$ref").GetString().Should().EndWith("/FormaAceite");
        JsonElement propriedades = schemas.GetProperty("FormaAceite").GetProperty("properties");
        propriedades.TryGetProperty("codigo", out _).Should().BeTrue();
        propriedades.TryGetProperty("rotulo", out _).Should().BeTrue();
    }
}
