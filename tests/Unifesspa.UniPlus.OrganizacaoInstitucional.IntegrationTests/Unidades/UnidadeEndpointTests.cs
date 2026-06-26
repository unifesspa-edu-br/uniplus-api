namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

/// <summary>
/// Smoke tests dos endpoints de <c>Unidade</c> (Story #586). Verificam
/// routing, vendor media type, HATEOAS, autenticação e autorização com
/// Wolverine rodando contra Postgres efêmero (<see cref="OrganizacaoEndpointFixture"/>).
/// </summary>
[Collection(OrganizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class UnidadeEndpointTests
{
    private readonly OrganizacaoEndpointFixture _fixture;

    public UnidadeEndpointTests(OrganizacaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades retorna 200 com Content-Type vendor MIME de unidade")]
    public async Task Listar_Retorna200ComVendorMime()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/organizacao/unidades", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.unidade.v1+json");
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades retorna array JSON (catálogo vazio)")]
    public async Task Listar_RetornaArrayJson()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/organizacao/unidades", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades/{id} retorna 404 quando unidade inexistente")]
    public async Task ObterPorId_NaoExiste_Retorna404()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/organizacao/unidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/unidades sem Idempotency-Key retorna 400")]
    public async Task Criar_SemIdempotencyKey_Retorna400()
    {
        // Auth precisa passar (Authorize roda antes dos filtros MVC).
        // [RequiresIdempotencyKey] é um filtro MVC — retorna 400 antes de
        // atingir o action quando o header está ausente.
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/organizacao/admin/unidades", UriKind.Relative));
        request.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        request.Headers.Add(TestAuthHandler.RolesHeader, "plataforma-admin");
        // Sem Idempotency-Key — esperamos 400 do filtro.
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Idempotency-Key é obrigatório — sem ela o filtro retorna 400 antes do action");
    }

    [Fact(DisplayName = "POST /api/organizacao/admin/unidades sem autenticação retorna 401")]
    public async Task Criar_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri("/api/organizacao/admin/unidades", UriKind.Relative));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "DELETE /api/organizacao/admin/unidades/{id} sem autenticação retorna 401")]
    public async Task Remover_SemAuth_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        HttpResponseMessage response = await client.DeleteAsync(
            new Uri($"/api/organizacao/admin/unidades/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?q= filtra server-side por sigla/nome/código/slug/alias")]
    public async Task Listar_ComBusca_FiltraServerSide()
    {
        string token = Guid.NewGuid().ToString("N")[..10];
        string siglaAlvo = $"{token}al";
        string siglaForaDoFiltro = Guid.NewGuid().ToString("N")[..10];

        await SemearUnidadeAsync(nome: "Centro Filtravel", sigla: siglaAlvo, tipo: TipoUnidade.Centro);
        await SemearUnidadeAsync(nome: "Faculdade Sem Marca", sigla: siglaForaDoFiltro, tipo: TipoUnidade.Faculdade);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/organizacao/unidades?q={token}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        IReadOnlyList<string> siglas = await LerSiglasAsync(response);
        siglas.Should().Contain(siglaAlvo.ToUpperInvariant());
        siglas.Should().NotContain(siglaForaDoFiltro.ToUpperInvariant());
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades?tipo=<inválido> retorna 400")]
    public async Task Listar_ComTipoInvalido_Retorna400()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            new Uri("/api/organizacao/unidades?tipo=999", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "GET /api/organizacao/unidades preserva q/tipo no header Link (self e next)")]
    public async Task Listar_ComFiltros_PreservaQueryParamsNoLink()
    {
        string token = Guid.NewGuid().ToString("N")[..10];
        // Dois Centro com o token → com limit=1 há próxima página (rel=next).
        await SemearUnidadeAsync(nome: $"Centro {token} Um", sigla: Guid.NewGuid().ToString("N")[..10], tipo: TipoUnidade.Centro);
        await SemearUnidadeAsync(nome: $"Centro {token} Dois", sigla: Guid.NewGuid().ToString("N")[..10], tipo: TipoUnidade.Centro);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri($"/api/organizacao/unidades?q={token}&tipo=3&limit=1", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Link", out IEnumerable<string>? links).Should().BeTrue();
        string link = links!.Single();
        link.Should().Contain("rel=\"next\"", "limit=1 com 2 itens filtrados deve emitir próxima página");
        link.Should().Contain($"q={token}", "o filtro de busca deve viajar em self e next");
        link.Should().Contain("tipo=3", "o filtro de tipo deve viajar em self e next");
    }

    private async Task SemearUnidadeAsync(string nome, string sigla, TipoUnidade tipo)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8];
        Unidade unidade = Unidade.Criar(
            nome,
            null,
            Slug.From($"u-{sufixo}").Value!,
            sigla,
            sufixo,
            null,
            tipo,
            false,
            new DateOnly(2026, 1, 1),
            null,
            OrigemUnidade.CriadoNoUniPlus).Value!;

        using IServiceScope scope = _fixture.Factory.Services.CreateScope();
        OrganizacaoInstitucionalDbContext ctx =
            scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
        ctx.Unidades.Add(unidade);
        await ctx.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<string>> LerSiglasAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        List<string> siglas = [];
        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("sigla", out JsonElement sigla) && sigla.GetString() is { } valor)
            {
                siglas.Add(valor);
            }
        }

        return siglas;
    }
}
