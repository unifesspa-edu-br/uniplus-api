namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.RegistrosDeAcesso;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O registro do acesso leva o client OIDC do token (claim <c>azp</c>), e a ausência do claim não muda a decisão.
/// Diogo Souza é gestor de acessos.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class ClienteOidcDoAcessoTests
{
    private readonly AutorizacaoEndpointFixture _fixture;

    public ClienteOidcDoAcessoTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "O registro do acesso traz o cliente OIDC do token, inclusive quando ausente")]
    [InlineData("selecao-web")]
    [InlineData(null)]
    public async Task Acesso_RegistraOClienteDoToken(string? azp)
    {
        using CenarioDeAcesso cenario = new(_fixture, azp);
        await cenario.ExecutarAsync();
        cenario.Resposta.StatusCode.Should().Be(HttpStatusCode.OK, await cenario.Resposta.Content.ReadAsStringAsync());
        cenario.Decisoes.Should().NotBeEmpty().And.OnlyContain(r => r.ClienteOidc == azp && r.Permitido);
    }

    private sealed class CenarioDeAcesso : IDisposable
    {
        private readonly AutorizacaoEndpointFixture _fixture;
        private readonly HttpClient _cliente;
        private readonly string _correlacao = $"req-{Guid.NewGuid():N}";

        public CenarioDeAcesso(AutorizacaoEndpointFixture fixture, string? azp)
        {
            _fixture = fixture;
            _cliente = fixture.Factory.CreateClient();
            _cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            _cliente.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "diogo.souza");
            _cliente.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "diogo.souza");
            _cliente.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
            _cliente.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, _correlacao);
            if (azp is not null)
                _cliente.DefaultRequestHeaders.Add(TestAuthHandler.ClienteOidcHeader, azp);
        }

        public HttpResponseMessage Resposta { get; private set; } = null!;
        public List<RegistroDecisaoAcesso> Decisoes { get; private set; } = [];

        public async Task ExecutarAsync()
        {
            await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, "diogo.souza", TipoDeGovernanca.GestorDeAcessos);
            Resposta = await _cliente.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative));
            Decisoes = [.. _fixture.Factory.Services.GetRequiredService<RegistroDeDecisoesDeTeste>()
                .Registrados.Where(r => r.RequestId == _correlacao)];
        }

        public void Dispose()
        {
            Resposta?.Dispose();
            _cliente.Dispose();
        }
    }
}
