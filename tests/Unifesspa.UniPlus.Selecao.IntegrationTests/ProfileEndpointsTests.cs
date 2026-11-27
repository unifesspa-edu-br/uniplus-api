namespace Unifesspa.UniPlus.Selecao.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

public sealed class ProfileEndpointsTests : IClassFixture<SelecaoApiFactory>
{
    private static readonly Uri GetMeUri = new("/api/profile/me", UriKind.Relative);
    private readonly SelecaoApiFactory _factory;

    public ProfileEndpointsTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMe_ShouldReturnProblemDetails_WhenRequestDoesNotHaveToken()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement root = payload.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(401);
        root.GetProperty("type").GetString()
            .Should().Be("https://unifesspa-edu-br.github.io/uniplus-developers/erros/uniplus.auth.unauthorized");
        root.GetProperty("code").GetString().Should().Be("uniplus.auth.unauthorized");
    }

    [Fact]
    public async Task GetMe_ShouldReturnProfile_WithCpfAndNomeSocial_WhenTokenIncludesUniPlusScope()
    {
        using WebApplicationFactory<Unifesspa.UniPlus.Host.HostAssemblyMarker> factory = ComConcessoes();
        using HttpClient client = factory.CreateClient();
        IdentificarCandidata(client);

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        ConferirDadosDaCandidata(payload.RootElement);
    }

    private static void Autenticar(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme,
            TestAuthHandler.TokenValue);

    private static void IdentificarCandidata(HttpClient client)
    {
        Autenticar(client);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "4dc27b1b-c56f-488a-a7a2-2a30bdbc08b8");
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "Maria Santos");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "maria@teste.unifesspa.edu.br");
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "candidato");
        client.DefaultRequestHeaders.Add(TestAuthHandler.CpfHeader, "529.982.247-25");
        client.DefaultRequestHeaders.Add(TestAuthHandler.NomeSocialHeader, "Maria dos Santos");
    }

    private static void ConferirDadosDaCandidata(JsonElement perfil)
    {
        perfil.GetProperty("userId").GetString().Should().Be("4dc27b1b-c56f-488a-a7a2-2a30bdbc08b8");
        perfil.GetProperty("name").GetString().Should().Be("Maria Santos");
        perfil.GetProperty("email").GetString().Should().Be("maria@teste.unifesspa.edu.br");
        perfil.GetProperty("cpf").GetString().Should().Be("529.982.247-25");
        perfil.GetProperty("nomeSocial").GetString().Should().Be("Maria dos Santos");
        perfil.TryGetProperty("roles", out _).Should().BeFalse("o papel do token não é perfil de acesso");
        perfil.GetProperty("perfis").GetArrayLength().Should().Be(0);
        perfil.GetProperty("timestamp").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetMe_ShouldReturnNullNomeSocial_WhenClaimAbsent()
    {
        using WebApplicationFactory<Unifesspa.UniPlus.Host.HostAssemblyMarker> factory = ComConcessoes();
        using HttpClient client = factory.CreateClient();
        Autenticar(client);

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        payload.RootElement.GetProperty("cpf").GetString().Should().Be(TestAuthHandler.CpfPadrao);
        payload.RootElement.GetProperty("nomeSocial").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetMe_ShouldReturnGrantedProfiles_WithScopeUnitAndExpiry()
    {
        Guid unidadeId = Guid.CreateVersion7();
        using WebApplicationFactory<Unifesspa.UniPlus.Host.HostAssemblyMarker> factory =
            ComConcessoesDeInstituicaoEUnidade(unidadeId, out ConcessaoAtiva daUnidade);
        using HttpClient client = factory.CreateClient();
        Autenticar(client);

        HttpResponseMessage response = await client.GetAsync(GetMeUri);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        JsonElement[] perfis = [.. payload.RootElement.GetProperty("perfis").EnumerateArray()];

        ConferirPerfisConcedidos(perfis, daUnidade, unidadeId);
    }

    private WebApplicationFactory<Unifesspa.UniPlus.Host.HostAssemblyMarker> ComConcessoesDeInstituicaoEUnidade(
        Guid unidadeId, out ConcessaoAtiva daUnidade)
    {
        ConcessaoAtiva institucional = new(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "Consulta institucional", null, true, null, []);
        daUnidade = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), null, "Gestor de processo seletivo", unidadeId, false, DateTimeOffset.UtcNow.AddDays(30), []);
        return ComConcessoes(
            [daUnidade, institucional],
            new UnidadeView(unidadeId, "CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", null, "Centro", true, null));
    }

    private static void ConferirPerfisConcedidos(JsonElement[] perfis, ConcessaoAtiva daUnidade, Guid unidadeId)
    {
        perfis.Select(static p => p.GetProperty("nome").GetString())
            .Should().Equal("Consulta institucional", "Gestor de processo seletivo");
        perfis[0].GetProperty("escopoTipo").GetProperty("codigo").GetString().Should().Be("INSTITUICAO");
        perfis[0].GetProperty("unidadeSigla").ValueKind.Should().Be(JsonValueKind.Null);
        perfis[1].GetProperty("concessaoId").GetGuid().Should().Be(daUnidade.ConcessaoId);
        perfis[1].GetProperty("escopoTipo").GetProperty("codigo").GetString().Should().Be("UNIDADE");
        perfis[1].GetProperty("unidadeId").GetGuid().Should().Be(unidadeId);
        perfis[1].GetProperty("unidadeSigla").GetString().Should().Be("CEPS");
        perfis[1].GetProperty("unidadeNome").GetString().Should().Be("Centro de Processos Seletivos");
        perfis[1].GetProperty("validaAte").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
    }

    // A fábrica sobe sem banco: as concessões e as unidades vêm de dublês.
    private WebApplicationFactory<Unifesspa.UniPlus.Host.HostAssemblyMarker> ComConcessoes(
        ConcessaoAtiva[]? concessoes = null,
        params UnidadeView[] unidades) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IConcessoesAtivasDoSujeito>();
                services.AddScoped<IConcessoesAtivasDoSujeito>(_ => new ConcessoesFixas(concessoes ?? []));
                services.RemoveAll<IUnidadeReader>();
                services.AddScoped<IUnidadeReader>(_ => new UnidadesFixas(unidades));
            }));

    private sealed class ConcessoesFixas(params ConcessaoAtiva[] concessoes) : IConcessoesAtivasDoSujeito
    {
        public Task<IReadOnlyList<ConcessaoAtiva>> ListarAsync(UsuarioRef pessoa, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ConcessaoAtiva>>(concessoes);
    }

    private sealed class UnidadesFixas(params UnidadeView[] unidades) : IUnidadeReader
    {
        public Task<IReadOnlyList<UnidadeView>> ListarAtivasAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnidadeView>>(unidades);

        public Task<UnidadeView?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(unidades.FirstOrDefault(u => u.Id == id));

        public Task<UnidadeView?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
            Task.FromResult(unidades.FirstOrDefault(u => u.Codigo == codigo));
    }
}
