namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Outbox.Cascading;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Quem tem a permissão de consultar os leiautes do INEP numa unidade vê a lista para escolher o leiaute do envio e
/// da importação das notas. Outra permissão da unidade e a falta de concessão seguem recusadas.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class ConsultaDosLeiautesPelaUnidadeEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string Rota = "/api/selecao/leiautes-de-arquivo-do-inep";

    private readonly CascadingFixture _fixture;

    public ConsultaDosLeiautesPelaUnidadeEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    [Fact(DisplayName = "A concessão de consultar na unidade abre a lista de leiautes")]
    public async Task Listar_ComConcessaoNaUnidade_200()
    {
        await ConcederNaUnidadeAsync(PermissaoSelecao.LeiautesInepConsultarNaUnidade);

        using HttpResponseMessage resposta = await ListarComoLeandroAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Sem concessão a lista de leiautes responde 403")]
    public async Task Listar_SemConcessao_403()
    {
        using HttpResponseMessage resposta = await ListarComoLeandroAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "Outra permissão da unidade não abre a lista de leiautes")]
    public async Task Listar_ComOutraPermissaoNaUnidade_403()
    {
        await ConcederNaUnidadeAsync(PermissaoSelecao.EnviosInepGerar);

        using HttpResponseMessage resposta = await ListarComoLeandroAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task ConcederNaUnidadeAsync(PermissaoSelecao permissao)
    {
        IServiceProvider servicos = _fixture.Factory.Services;
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(servicos, permissao.Codigo);
        await ConcessoesDeTeste.ConcederAsync(servicos, Leandro, perfil, EscopoDaConcessao.DaUnidade(Guid.NewGuid()));
    }

    private async Task<HttpResponseMessage> ListarComoLeandroAsync()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Leandro);
        return await client.SendAsync(request);
    }
}
