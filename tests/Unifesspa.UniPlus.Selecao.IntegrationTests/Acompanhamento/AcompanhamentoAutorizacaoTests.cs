namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Acompanhamento;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Pagination;

public sealed class AcompanhamentoAutorizacaoTests : IClassFixture<SelecaoApiFactory>
{
    private readonly SelecaoApiFactory _factory;

    public AcompanhamentoAutorizacaoTests(SelecaoApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Leituras_SemToken_Retornam401()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage lista = await client.GetAsync("/api/selecao/acompanhamento/processos-seletivos");
        using HttpResponseMessage detalhe = await client.GetAsync(
            $"/api/selecao/acompanhamento/processos-seletivos/{Guid.NewGuid():D}");

        lista.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        detalhe.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpcaoDesligada_UsuarioAutenticadoRecebe403EConsultaAdministrativaMantemRestricao()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);

        using HttpResponseMessage lista = await client.GetAsync("/api/selecao/acompanhamento/processos-seletivos");
        using HttpResponseMessage detalhe = await client.GetAsync(
            $"/api/selecao/acompanhamento/processos-seletivos/{Guid.NewGuid():D}");
        using HttpResponseMessage cadastro = await client.GetAsync("/api/selecao/processos-seletivos");

        lista.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        detalhe.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        cadastro.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SegmentoDeIdInvalido_NaoCasaComRotaGuid()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);

        using HttpResponseMessage resposta = await client.GetAsync(
            "/api/selecao/acompanhamento/processos-seletivos/nao-e-uuid");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CursorDoCadastro_ERecusadoNaColecaoDeAcompanhamento()
    {
        CursorEncoder encoder = _factory.Services.GetRequiredService<CursorEncoder>();
        string cursorDoCadastro = await encoder.EncodeAsync(new CursorPayload(
            Guid.CreateVersion7().ToString(),
            Limit: 20,
            ResourceTag: "processos-seletivos",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5),
            Direction: PaginationDirection.Next));

        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);

        using HttpResponseMessage resposta = await client.GetAsync(
            $"/api/selecao/acompanhamento/processos-seletivos?cursor={Uri.EscapeDataString(cursorDoCadastro)}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LimiteForaDoIntervalo_Retorna422()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);

        using HttpResponseMessage resposta = await client.GetAsync(
            "/api/selecao/acompanhamento/processos-seletivos?limit=101");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task MediaTypeIncompativel_Retorna406()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        using HttpResponseMessage resposta = await client.GetAsync(
            "/api/selecao/acompanhamento/processos-seletivos");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
