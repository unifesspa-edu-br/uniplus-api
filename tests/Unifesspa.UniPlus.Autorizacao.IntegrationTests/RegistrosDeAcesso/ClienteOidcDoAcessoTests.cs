namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.RegistrosDeAcesso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O registro do acesso leva o client OIDC do token (claim <c>azp</c>), e a ausência do claim não muda a decisão.
/// Diogo Souza é gestor de acessos.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ClienteOidcDoAcessoTests
{
    private readonly AutorizacaoEndpointFixture _fixture;

    public ClienteOidcDoAcessoTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "O registro do acesso traz o azp do token e, sem ele, o cliente vai nulo com o acesso permitido")]
    [InlineData("selecao-web")]
    [InlineData(null)]
    public async Task Acesso_RegistraOClienteDoToken(string? azp)
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, "diogo.souza", TipoDeGovernanca.GestorDeAcessos);
        using HttpClient diogo = _fixture.Factory.CreateClient();
        diogo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        diogo.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "diogo.souza");
        diogo.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "diogo.souza");
        diogo.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        string correlacao = $"req-{Guid.NewGuid():N}";
        diogo.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlacao);
        if (azp is not null)
        {
            diogo.DefaultRequestHeaders.Add(TestAuthHandler.ClienteOidcHeader, azp);
        }

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        List<RegistroDecisaoAcesso> doAcesso = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>()
            .Registrados.Where(r => r.RequestId == correlacao)];
        doAcesso.Should().NotBeEmpty().And.OnlyContain(r => r.ClienteOidc == azp && r.Permitido);
    }
}
