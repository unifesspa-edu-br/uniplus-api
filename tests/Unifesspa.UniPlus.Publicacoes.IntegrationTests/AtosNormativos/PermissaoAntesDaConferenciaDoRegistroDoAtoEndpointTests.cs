namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

/// <summary>
/// Quem não tem a permissão de registrar atos recebe a recusa de permissão antes de o corpo ser conferido: a resposta
/// não traz nenhuma das recusas de campo, ainda que o corpo tenha vários campos inválidos.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class PermissaoAntesDaConferenciaDoRegistroDoAtoEndpointTests : IAsyncLifetime
{
    private const string Conta = "marcelo.souza";

    private readonly PublicacoesEndpointFixture _fixture;

    public PermissaoAntesDaConferenciaDoRegistroDoAtoEndpointTests(PublicacoesEndpointFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Conta);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Sem a permissão, o corpo com vários campos inválidos recebe 403 sem recusa de campo")]
    public async Task Registrar_SemPermissaoECorpoInvalido_Retorna403SemRecusaDeCampo()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = new(HttpMethod.Post, new Uri("/api/publicacoes/admin/atos", UriKind.Relative))
        {
            Content = JsonContent.Create(new { orgao = "", serie = new string('A', 101), ano = 0, documentoHash = "invalido", vinculos = new object?[] { null } }),
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        requisicao.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Conta);

        using HttpResponseMessage resposta = await client.SendAsync(requisicao);

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().NotContain("uniplus.publicacoes.ato_normativo.").And.NotContain("\"field\"");
    }
}
