namespace Unifesspa.UniPlus.Geo.IntegrationTests.Admin;

using System.Net;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// Endpoint admin de disparo/acompanhamento do ETL (Story #674, CA-02/CA-06) contra a
/// API real. O worker fica desligado (<c>GeoApiFactory</c>), então o disparo registra a
/// execução EmAndamento e responde 202 sem rodar a carga — tornando 202/409
/// determinísticos. Autorização por role <c>plataforma-admin</c> (401/403).
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class GeoImportacoesEndpointTests
{
    private const string Rota = "/api/admin/geo/importacoes";

    private readonly GeoPostgisFixture _fixture;

    public GeoImportacoesEndpointTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-02: POST com plataforma-admin retorna 202, Location e registra a execução EmAndamento")]
    public async Task Disparar_ComAdmin_Retorna202_E_Registra()
    {
        await LimparExecucoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage disparo = RequisicaoDisparo("202601", admin: true);
        using HttpResponseMessage resposta = await client.SendAsync(disparo);

        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        resposta.Headers.Location.Should().NotBeNull();

        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.GetProperty("status").GetString().Should().Be("EmAndamento");
        corpo.RootElement.GetProperty("versaoDataset").GetString().Should().Be("202601");
        Guid id = corpo.RootElement.GetProperty("id").GetGuid();

        using HttpRequestMessage consulta = RequisicaoGet(id, admin: true);
        using HttpResponseMessage acompanhamento = await client.SendAsync(consulta);
        acompanhamento.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "CA-02: POST sem autenticação retorna 401")]
    public async Task Disparar_SemAutenticacao_Retorna401()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage requisicao = new(HttpMethod.Post, Rota)
        {
            Content = CorpoJson("202601"),
        };
        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "CA-02: POST autenticado sem o role plataforma-admin retorna 403")]
    public async Task Disparar_SemRole_Retorna403()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage requisicao = RequisicaoDisparo("202601", admin: false);
        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "CA-06: com uma carga em andamento, um segundo disparo retorna 409 (sem Idempotency-Key)")]
    public async Task Disparar_ComCargaEmAndamento_Retorna409()
    {
        await LimparExecucoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage disparo1 = RequisicaoDisparo("202601", admin: true);
        using HttpResponseMessage primeira = await client.SendAsync(disparo1);
        primeira.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using HttpRequestMessage disparo2 = RequisicaoDisparo("202602", admin: true);
        using HttpResponseMessage segunda = await client.SendAsync(disparo2);
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory(DisplayName = "CA-02: versão fora do formato AAAAMM (ou ausente) é rejeitada na borda com 422")]
    [InlineData("209913")]
    [InlineData("20260")]
    [InlineData("")]
    public async Task Disparar_VersaoInvalida_Retorna422(string versao)
    {
        await LimparExecucoesAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpRequestMessage requisicao = RequisicaoDisparo(versao, admin: true);
        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "CA-03: versão anterior à última release concluída é recusada com 409 (não rebaixa o dataset)")]
    public async Task Disparar_VersaoAnteriorAUltimaConcluida_Retorna409()
    {
        await LimparExecucoesAsync();
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            GeoImportacaoExecucao concluida = GeoImportacaoExecucao
                .Iniciar("202602", "teste", TimeProvider.System.GetUtcNow()).Value!;
            concluida.Concluir(TimeProvider.System.GetUtcNow(), "{}", "ok");
            ctx.ImportacaoExecucoes.Add(concluida);
            await ctx.SaveChangesAsync();
        }

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoDisparo("202601", admin: true);
        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "CA-03: versão anterior a uma release que falhou (pode ter dados parciais) também é recusada com 409")]
    public async Task Disparar_VersaoAnteriorAUltimaFalha_Retorna409()
    {
        await LimparExecucoesAsync();
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            GeoImportacaoExecucao falhou = GeoImportacaoExecucao
                .Iniciar("202602", "teste", TimeProvider.System.GetUtcNow()).Value!;
            falhou.Falhar(TimeProvider.System.GetUtcNow(), "falha parcial nas folhas");
            ctx.ImportacaoExecucoes.Add(falhou);
            await ctx.SaveChangesAsync();
        }

        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = RequisicaoDisparo("202601", admin: true);
        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static HttpRequestMessage RequisicaoDisparo(string versao, bool admin)
    {
        HttpRequestMessage requisicao = new(HttpMethod.Post, Rota) { Content = CorpoJson(versao) };
        Autenticar(requisicao, admin);
        return requisicao;
    }

    private static HttpRequestMessage RequisicaoGet(Guid id, bool admin)
    {
        HttpRequestMessage requisicao = new(HttpMethod.Get, $"{Rota}/{id}");
        Autenticar(requisicao, admin);
        return requisicao;
    }

    private static void Autenticar(HttpRequestMessage requisicao, bool admin)
    {
        requisicao.Headers.Add("Authorization", $"{TestAuthHandler.AuthorizationScheme} {TestAuthHandler.TokenValue}");
        requisicao.Headers.Add(TestAuthHandler.RolesHeader, admin ? "plataforma-admin" : "operador-geo");
    }

    private static StringContent CorpoJson(string versao) =>
        new($"{{\"versao\":\"{versao}\"}}", Encoding.UTF8, "application/json");

    private async Task LimparExecucoesAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE geo_importacao_execucao");
    }
}
