namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Acompanhamento;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class AcompanhamentoAutorizacaoTests : IClassFixture<SelecaoApiFactory>
{
    private const string Colecao = "/api/selecao/acompanhamento/processos-seletivos";

    private readonly SelecaoApiFactory _factory;

    public AcompanhamentoAutorizacaoTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Leituras_SemToken_Retornam401()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage lista = await client.GetAsync(Colecao);
        using HttpResponseMessage detalhe = await client.GetAsync($"{Colecao}/{Guid.NewGuid():D}");

        lista.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        detalhe.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpcaoDesligada_UsuarioAutenticadoRecebe403EConsultaAdministrativaMantemRestricao()
    {
        using HttpClient client = ClienteAutenticado(_factory);

        using HttpResponseMessage lista = await client.GetAsync(Colecao);
        using HttpResponseMessage detalhe = await client.GetAsync($"{Colecao}/{Guid.NewGuid():D}");
        using HttpResponseMessage cadastro = await client.GetAsync("/api/selecao/processos-seletivos");

        lista.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        detalhe.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        cadastro.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("?limit=101")]
    [InlineData("?direction=lateral")]
    [InlineData("?cursor=nao-e-cursor")]
    public async Task OpcaoDesligada_ParametroInvalidoTambemRecebe403(string consulta)
    {
        using HttpClient client = ClienteAutenticado(_factory);

        using HttpResponseMessage resposta = await client.GetAsync($"{Colecao}{consulta}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OpcaoLigada_CadastroContinuaRestritoAoAdministrador()
    {
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_factory);
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage cadastro = await client.GetAsync("/api/selecao/processos-seletivos");

        cadastro.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OpcaoLigada_SegmentoDeIdInvalido_NaoCasaComRotaGuid()
    {
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_factory);
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage resposta = await client.GetAsync($"{Colecao}/nao-e-uuid");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OpcaoLigada_CursorDoCadastro_ERecusado()
    {
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_factory);
        CursorEncoder encoder = habilitada.Services.GetRequiredService<CursorEncoder>();
        string cursorDoCadastro = await encoder.EncodeAsync(new CursorPayload(
            Guid.CreateVersion7().ToString(),
            Limit: 20,
            ResourceTag: "processos-seletivos",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5),
            Direction: PaginationDirection.Next));
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage resposta = await client.GetAsync(
            $"{Colecao}?cursor={Uri.EscapeDataString(cursorDoCadastro)}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpcaoLigada_LimiteForaDoIntervalo_Retorna422()
    {
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_factory);
        using HttpClient client = ClienteAutenticado(habilitada);

        using HttpResponseMessage resposta = await client.GetAsync($"{Colecao}?limit=101");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task OpcaoLigada_MediaTypeIncompativel_Retorna406()
    {
        await using WebApplicationFactory<HostAssemblyMarker> habilitada = ComValidacaoHabilitada(_factory);
        using HttpClient client = ClienteAutenticado(habilitada);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        using HttpResponseMessage resposta = await client.GetAsync(Colecao);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    internal static WebApplicationFactory<HostAssemblyMarker> ComValidacaoHabilitada(
        WebApplicationFactory<HostAssemblyMarker> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuracao) =>
            configuracao.AddInMemoryCollection(
                [new KeyValuePair<string, string?>("Selecao:Acompanhamento:ValidacaoHabilitada", "true")])));

    internal static HttpClient ClienteAutenticado(WebApplicationFactory<HostAssemblyMarker> factory)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        return client;
    }
}
