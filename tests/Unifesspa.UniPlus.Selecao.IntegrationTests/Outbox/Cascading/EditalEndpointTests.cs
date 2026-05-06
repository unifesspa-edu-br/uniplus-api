namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Kernel.Results;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Cobertura HTTP direta de <c>POST /api/editais</c> (criação) e
/// <c>GET /api/editais/{id}</c> (consulta) — endpoints que ganharam
/// roteamento pelo fix de #173 mas não tinham testes HTTP dedicados.
/// Issue #202.
/// </summary>
/// <remarks>
/// Reusa a <see cref="CascadingFixture"/> para ter Postgres efêmero
/// disponível, mas os cenários aqui não dependem de cascading messages
/// (handler de criação não emite domain events além do fluxo já testado
/// em <see cref="PublicarEditalEndpointTests"/>). Pareada à mesma
/// collection para evitar paralelismo inválido com PG compartilhado.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class EditalEndpointTests
{
    private static int _numeroSeedCounter;

    private static int NextNumeroSeed() =>
        (Interlocked.Increment(ref _numeroSeedCounter) % 9999) + 1;

    private readonly CascadingFixture _fixture;

    public EditalEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "POST /api/editais retorna 201 + Location quando o command é válido")]
    public async Task CriarEdital_DeveRetornar201()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        int numero = NextNumeroSeed();
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/editais", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numeroEdital = numero,
                anoEdital = 2026,
                titulo = "EditalEndpointTests CriarEdital",
                tipoProcesso = (int)TipoProcesso.SiSU,
            }),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull(
            "[ApiController] + CreatedAtAction expõem Location apontando para o recurso criado");

        // O body do POST é o GUID do recurso criado.
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Guid editalId = doc.RootElement.GetGuid();
        editalId.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "GET /api/editais/{id} retorna 200 quando o edital existe")]
    public async Task ObterEdital_DeveRetornar200_QuandoExiste()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Edital edital = await SemearEditalAsync(api);

        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri($"/api/editais/{edital.Id}", UriKind.Relative));
        AppendTestAuth(request);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.edital.v1+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("id").GetGuid().Should().Be(edital.Id);
    }

    [Fact(DisplayName = "GET /api/editais/{id} retorna 404 quando o edital não existe")]
    public async Task ObterEdital_DeveRetornar404_QuandoNaoExiste()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Guid inexistente = Guid.CreateVersion7();
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri($"/api/editais/{inexistente}", UriKind.Relative));
        AppendTestAuth(request);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.edital.v1+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET /api/editais retorna Link e X-Page-Size headers (RFC 5988/8288, ADR-0026)")]
    public async Task ListarEditais_DeveExporLinkEXPageSizeHeaders()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        // Garante 2 editais no DB para forçar próxima página com limit=1.
        await SemearEditalAsync(api);
        await SemearEditalAsync(api);

        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri("/api/editais?limit=1", UriKind.Relative));
        AppendTestAuth(request);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.uniplus.edital.v1+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("X-Page-Size", out IEnumerable<string>? pageSizeValues).Should().BeTrue(
            "OkPaginatedAsync popula X-Page-Size com a contagem de itens da página atual (ADR-0026)");
        pageSizeValues!.Single().Should().Be("1", "limit=1 + ao menos 2 editais semeados garantem 1 item na página");

        response.Headers.TryGetValues("Link", out IEnumerable<string>? linkValues).Should().BeTrue(
            "OkPaginatedAsync popula Link header (RFC 5988/8288) com rel=\"self\" e — quando há próxima — rel=\"next\"");
        string linkHeader = linkValues!.Single();
        linkHeader.Should().Contain("rel=\"self\"");
        linkHeader.Should().Contain("rel=\"next\"",
            "ao menos 2 editais existem e limit=1 deve emitir cursor para a próxima página");
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    private static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, "test-edital-user");
    }

    private static async Task<Edital> SemearEditalAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        int numeroSeed = NextNumeroSeed();
        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(numero: numeroSeed, ano: 2026);
        numeroResult.IsSuccess.Should().BeTrue();
        Edital edital = Edital.Criar(numeroResult.Value!, "EditalEndpointTests seed", TipoProcesso.SiSU);
        edital.ClearDomainEvents();
        await db.Editais.AddAsync(edital);
        await db.SaveChangesAsync();
        return edital;
    }
}
