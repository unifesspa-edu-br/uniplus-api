namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Editais;

using System.Globalization;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Kernel.Results;
using Domain.Entities;
using Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Outbox.Cascading;

/// <summary>
/// Cobertura de <c>GET /api/selecao/editais/{id}</c> em runtime real, focando em
/// HATEOAS Level 1 (ADR-0029): body inclui <c>_links.self</c> sempre, e
/// <c>_links.collection</c>; nenhum action link (<c>publicar</c>, etc.) per
/// vedação explícita da ADR-0029.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ObterEditalEndpointTests
{
    private readonly CascadingFixture _fixture;

    public ObterEditalEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/selecao/editais/{id} retorna 200 + body com _links.self e _links.collection (ADR-0029)")]
    public async Task ObterPorId_EditalExistente_RetornaBodyComLinks()
    {
        Edital seeded = await SemearEditalAsync(_fixture.Factory);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/selecao/editais/{seeded.Id}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);

        // _links presente, com chave snake_case prefixada por underscore (ADR-0029).
        JsonElement links = doc.RootElement.GetProperty("_links");
        links.ValueKind.Should().Be(JsonValueKind.Object);

        // _links.self obrigatório, URI relativa apontando para o próprio recurso.
        string self = links.GetProperty("self").GetString()!;
        self.Should().Be(
            $"/api/selecao/editais/{seeded.Id.ToString("D", CultureInfo.InvariantCulture)}",
            "ADR-0029 §'URIs relativas': self é URI relativa à raiz da API");

        // _links.collection navega de volta para a coleção.
        string collection = links.GetProperty("collection").GetString()!;
        collection.Should().Be(
            "/api/selecao/editais",
            "ADR-0029 §'Forma do _links': collection aponta para o endpoint de listagem");

        // Action links (publicar etc.) NÃO aparecem em V1 — ADR-0029 §'Esta ADR não decide'.
        // Operações de mutação são descobertas via OpenAPI (ADR-0030).
        links.TryGetProperty("publicar", out _).Should().BeFalse(
            "ADR-0029 veda action links em V1; operações de mutação ficam no OpenAPI");

        // Exatamente 2 chaves em V1 (self + collection). Qualquer adição
        // silenciosa (incluindo action link via rota não-óbvia) falha aqui —
        // toda relação nova exige PR review + ADR-0049 supersede explícito.
        links.EnumerateObject().Count().Should().Be(2,
            "ADR-0049 V1 emite somente self e collection");

        await response.AssertNoPiiAsync();
    }

    [Fact(DisplayName = "GET /api/selecao/editais/{id} para id inexistente retorna 404 sem body de _links")]
    public async Task ObterPorId_EditalInexistente_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/selecao/editais/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<Edital> SemearEditalAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        int numeroSeed = Math.Abs(Guid.NewGuid().GetHashCode() % 9000) + 1;
        Result<NumeroEdital> numero = NumeroEdital.Criar(numero: numeroSeed, ano: 2026);
        numero.IsSuccess.Should().BeTrue();

        Edital edital = Edital.Criar(numero.Value!, "ObterEditalEndpointTests seed");
        edital.ClearDomainEvents();
        await db.Editais.AddAsync(edital);
        await db.SaveChangesAsync();

        return edital;
    }
}
