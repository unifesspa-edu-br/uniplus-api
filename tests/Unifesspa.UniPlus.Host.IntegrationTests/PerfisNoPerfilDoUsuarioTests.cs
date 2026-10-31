namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// <c>GET /api/profile/me</c> contra as concessões gravadas no banco: o perfil do usuário lista o que a
/// pessoa tem por concessão vigente e não traz papel do token.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class PerfisNoPerfilDoUsuarioTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Fernanda = "fernanda.drumond";

    private static readonly string PermissaoDeUnidade =
        CatalogoDePermissoes<PermissaoDeTeste>.Obter(PermissaoDeTeste.InscricoesOperar).Requisito.Permissao;

    private static readonly Uri PerfilUri = new("/api/profile/me", UriKind.Relative);

    private readonly MonolitoPostgresFixture _fixture;

    public PerfisNoPerfilDoUsuarioTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Leandro, Fernanda })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Pessoa sem concessão recebe perfis vazios e nenhum papel do token")]
    public async Task PessoaSemConcessao_RecebePerfisVazios()
    {
        using JsonDocument corpo = await ConsultarAsync(Fernanda, papeisDoToken: "candidato");

        corpo.RootElement.GetProperty("perfis").GetArrayLength().Should().Be(0);
        corpo.RootElement.TryGetProperty("roles", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Lista a concessão institucional com o perfil e sem unidade nem prazo")]
    public async Task ListaConcessaoInstitucional()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.MotivosDecisaoIsencaoManter.Codigo);
        Guid institucional = await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);

        using JsonDocument corpo = await ConsultarAsync(Leandro);

        JsonElement item = corpo.RootElement.GetProperty("perfis").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("concessaoId").GetGuid().Should().Be(institucional);
        item.GetProperty("perfilId").GetGuid().Should().Be(perfil);
        item.GetProperty("escopoTipo").GetString().Should().Be("instituicao");
        item.GetProperty("unidadeId").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("validaAte").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "Concessão em unidade que não existe mais continua listada, sem sigla nem nome")]
    public async Task ConcessaoEmUnidadeInexistente_ContinuaListada()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoDeUnidade);
        Guid unidadeExtinta = Guid.CreateVersion7();
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, Leandro, perfil, EscopoDaConcessao.DaUnidade(unidadeExtinta));

        using JsonDocument corpo = await ConsultarAsync(Leandro);

        JsonElement item = corpo.RootElement.GetProperty("perfis").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("escopoTipo").GetString().Should().Be("unidade");
        item.GetProperty("unidadeId").GetGuid().Should().Be(unidadeExtinta);
        item.GetProperty("unidadeSigla").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("unidadeNome").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "Concessão revogada e concessão de outra pessoa não aparecem")]
    public async Task ConcessaoRevogadaEDeOutraPessoa_NaoAparecem()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.MotivosDecisaoIsencaoManter.Codigo);
        Guid revogada = await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Fernanda, perfil);
        await RevogarAsync(revogada);

        using JsonDocument corpo = await ConsultarAsync(Leandro);

        corpo.RootElement.GetProperty("perfis").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName = "Perfil desativado depois da concessão continua listado")]
    public async Task PerfilDesativadoComConcessaoVigente_ContinuaListado()
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.MotivosDecisaoIsencaoManter.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, Leandro, perfil);
        await DesativarAsync(perfil);

        using JsonDocument corpo = await ConsultarAsync(Leandro);

        corpo.RootElement.GetProperty("perfis").GetArrayLength().Should().Be(1);
    }

    private async Task<JsonDocument> ConsultarAsync(string sub, string? papeisDoToken = null)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        if (papeisDoToken is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, papeisDoToken);
        }

        using HttpResponseMessage response = await client.GetAsync(PerfilUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private async Task RevogarAsync(Guid concessaoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        Concessao concessao = await db.Concessoes.SingleAsync(c => c.Id == concessaoId);
        concessao.Revogar(
            new Identidade(TestAuthHandler.DefaultIssuer, "revogador.de.teste"),
            "revogada no teste",
            scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow());
        await db.SaveChangesAsync();
    }

    private async Task DesativarAsync(Guid perfilId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        AutorizacaoDbContext db = scope.ServiceProvider.GetRequiredService<AutorizacaoDbContext>();
        Perfil perfil = await db.Perfis.SingleAsync(p => p.Id == perfilId);
        perfil.Desativar();
        await db.SaveChangesAsync();
    }
}
