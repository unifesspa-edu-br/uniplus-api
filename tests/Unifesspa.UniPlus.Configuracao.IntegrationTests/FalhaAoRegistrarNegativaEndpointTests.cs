namespace Unifesspa.UniPlus.Configuracao.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Com o registro de acesso indisponível, a negativa de um endpoint de dado interno responde 503 em vez de 403, e quem tem
/// a permissão continua trabalhando, porque o permitido abaixo de dado pessoal não toca o registro.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FalhaAoRegistrarNegativaEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";

    private readonly ConfiguracaoEndpointFixture _fixture;

    public FalhaAoRegistrarNegativaEndpointTests(ConfiguracaoEndpointFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Sem a permissão num endpoint de dado interno, o registro indisponível faz a negativa responder 503")]
    public async Task Negativa_RegistroIndisponivel_503()
    {
        using HttpResponseMessage resposta = await EnviarComRegistroIndisponivelAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact(DisplayName = "Com a permissão num endpoint de dado interno, o registro indisponível não é tocado e a resposta é a normal")]
    public async Task PermitidoInterno_RegistroIndisponivel_RespostaNormal()
    {
        await ConcessoesDeTeste.ConcederPermissoesAsync(_fixture.Factory.Services, Leandro, PermissaoConfiguracao.CampiManter.Codigo);

        using HttpResponseMessage resposta = await EnviarComRegistroIndisponivelAsync();

        resposta.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable).And.NotBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> EnviarComRegistroIndisponivelAsync()
    {
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));
        using HttpClient client = comFalha.CreateClient();
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/configuracao/admin/{PermissaoConfiguracao.CampiManter.Resource}", UriKind.Relative))
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, Leandro);

        return await client.SendAsync(request);
    }

    private sealed class RegistroQueFalha : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker fora"));
    }
}
