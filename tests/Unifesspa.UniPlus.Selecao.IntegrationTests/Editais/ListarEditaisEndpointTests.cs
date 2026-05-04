namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Editais;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class ListarEditaisEndpointTests
{
    private readonly CascadingFixture _fixture;

    public ListarEditaisEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/editais retorna 200, body JSON array, header Link com rel=\"self\"")]
    public async Task Listar_SemCursor_RetornaArrayJsonComLinkSelf()
    {
        await SemearEditaisAsync(_fixture.Factory, quantidade: 2);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/editais?limit=10", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/vnd.uniplus.edital.v1+json");
        response.Headers.Should().Contain(h => h.Key == "Link");

        string linkHeader = string.Join(',', response.Headers.GetValues("Link"));
        linkHeader.Should().Contain("rel=\"self\"");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);

        await response.AssertNoPiiAsync();
    }

    [Fact(DisplayName = "GET /api/editais com cursor adulterado retorna 400 cursor_invalido")]
    public async Task Listar_CursorAdulterado_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/editais?cursor=AAAA-cursor-invalido-AAAA", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.cursor_invalido");

        await response.AssertNoPiiAsync();
    }

    [Fact(DisplayName = "GET /api/editais com Accept de versao inexistente retorna 406 + available_versions")]
    public async Task Listar_AcceptVersaoInexistente_Retorna406()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/editais", UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.edital.v9+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.contract.versao_nao_suportada");
        JsonElement versions = doc.RootElement.GetProperty("available_versions");
        versions.ValueKind.Should().Be(JsonValueKind.Array);
        versions.EnumerateArray().Select(static v => v.GetInt32()).Should().BeEquivalentTo(new[] { 1 });

        await response.AssertNoPiiAsync();
    }

    [Fact(DisplayName = "GET /api/editais com limit fora de faixa retorna 422 cursor_limit_invalido")]
    public async Task Listar_LimitInvalido_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/api/editais?limit=999", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.cursor_limit_invalido");
    }

    [Fact(DisplayName = "GET /api/editais ?limit do query string vence sobre limit do cursor")]
    public async Task Listar_LimitDoQueryStringVencePrecedencia()
    {
        await SemearEditaisAsync(_fixture.Factory, quantidade: 6);
        using HttpClient client = _fixture.Factory.CreateClient();

        // Primeira página com limit=2 — emite cursor com Limit=2 embutido.
        HttpResponseMessage paginaUm = await client.GetAsync(new Uri("/api/editais?limit=2", UriKind.Relative));
        paginaUm.StatusCode.Should().Be(HttpStatusCode.OK);
        string nextCursor = ExtrairCursorNext(paginaUm);

        // Reutiliza o cursor mas pede explicitamente limit=4 — query string deve vencer.
        HttpResponseMessage paginaDois = await client.GetAsync(
            new Uri($"/api/editais?cursor={Uri.EscapeDataString(nextCursor)}&limit=4", UriKind.Relative));
        paginaDois.StatusCode.Should().Be(HttpStatusCode.OK);

        IReadOnlyList<Guid> ids = await ExtrairIdsAsync(paginaDois);
        ids.Count.Should().BeLessThanOrEqualTo(4);
        ids.Count.Should().BeGreaterThan(2,
            "limit=4 do query string venceu sobre Limit=2 herdado do cursor");
    }

    [Fact(DisplayName = "GET /api/editais navega cursor sem duplicar nem omitir itens")]
    public async Task Listar_CursorNavegacao_PreservaJanela()
    {
        // Seed garante volume mínimo; o DB pode ter editais residuais de outros
        // testes da mesma collection — a invariante validada aqui é a estabilidade
        // do cursor (sem duplicação, sem omissão), não o conteúdo do seed.
        await SemearEditaisAsync(_fixture.Factory, quantidade: 5);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage paginaUm = await client.GetAsync(new Uri("/api/editais?limit=2", UriKind.Relative));
        paginaUm.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<Guid> idsPagina1 = await ExtrairIdsAsync(paginaUm);

        string nextCursor = ExtrairCursorNext(paginaUm);
        nextCursor.Should().NotBeNullOrEmpty();

        HttpResponseMessage paginaDois = await client.GetAsync(
            new Uri($"/api/editais?cursor={Uri.EscapeDataString(nextCursor)}", UriKind.Relative));
        paginaDois.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<Guid> idsPagina2 = await ExtrairIdsAsync(paginaDois);

        idsPagina1.Should().HaveCount(2);
        idsPagina2.Should().NotIntersectWith(idsPagina1);

        Guid[] uniao = [.. idsPagina1.Concat(idsPagina2)];
        uniao.Should().OnlyHaveUniqueItems();
    }

    private static async Task<IReadOnlyList<Guid>> ExtrairIdsAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return [.. doc.RootElement.EnumerateArray().Select(static element => element.GetProperty("id").GetGuid())];
    }

    private static string ExtrairCursorNext(HttpResponseMessage response)
    {
        string linkHeader = string.Join(',', response.Headers.GetValues("Link"));
        // Formato: <https://.../api/editais?cursor=ABC&limit=2>; rel="next", <...>; rel="self"
        int relIndex = linkHeader.IndexOf("rel=\"next\"", StringComparison.Ordinal);
        relIndex.Should().BeGreaterThan(-1);
        int urlEnd = linkHeader.LastIndexOf('>', relIndex);
        int urlStart = linkHeader.LastIndexOf('<', urlEnd);
        string url = linkHeader.Substring(urlStart + 1, urlEnd - urlStart - 1);
        Uri uri = new(url);
        string query = uri.Query.TrimStart('?');
        string cursorParam = query.Split('&').First(static p => p.StartsWith("cursor=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cursorParam["cursor=".Length..]);
    }

    private static async Task<IReadOnlyList<Edital>> SemearEditaisAsync(CascadingApiFactory api, int quantidade)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        List<Edital> seeded = new(quantidade);
        for (int i = 0; i < quantidade; i++)
        {
            int numeroSeed = Math.Abs((Guid.NewGuid().GetHashCode() + i) % 9000) + 1;
            Result<NumeroEdital> numero = NumeroEdital.Criar(numero: numeroSeed, ano: 2026);
            numero.IsSuccess.Should().BeTrue();
            Edital edital = Edital.Criar(numero.Value!, $"ListarEditaisEndpointTests seed {i}", TipoProcesso.SiSU);
            edital.ClearDomainEvents();
            await db.Editais.AddAsync(edital);
            seeded.Add(edital);
        }
        await db.SaveChangesAsync();

        return seeded;
    }
}
