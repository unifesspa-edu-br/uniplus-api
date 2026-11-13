namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// As requisições ao host de teste: da persona que o token identifica pelo <c>sub</c>, do administrador anônimo que
/// monta o processo e da persona atendida por um host cujo registro de acesso está fora do ar. O corpo vai em JSON, e o
/// verbo que muda estado leva a chave de idempotência.
/// </summary>
internal sealed class RequisicoesAutenticadas(CascadingFixture fixture)
{
    public const string PapelDeAdministrador = "plataforma-admin";

    public Task<HttpResponseMessage> GetAsync(string persona, string rota, string? papel = null) =>
        SendAsync(persona, HttpMethod.Get, rota, null, papel);

    public async Task<HttpResponseMessage> SendAsync(string persona, HttpMethod metodo, string rota, object? corpo, string? papel = null)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        return await client.SendAsync(DaPersona(persona, metodo, rota, corpo, papel));
    }

    public Task<HttpResponseMessage> SendAdminAsync(HttpMethod metodo, string rota, object corpo)
    {
        HttpClient client = fixture.Factory.CreateClient();
        HttpRequestMessage request = Requisicao(metodo, rota, corpo);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, PapelDeAdministrador);
        ExigirChaveDeIdempotencia(request);
        return client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> GetComRegistroIndisponivelAsync(string persona, string rota)
    {
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));
        using HttpClient client = comFalha.CreateClient();

        return await client.SendAsync(DaPersona(persona, HttpMethod.Get, rota, null, null));
    }

    private static HttpRequestMessage DaPersona(string persona, HttpMethod metodo, string rota, object? corpo, string? papel)
    {
        HttpRequestMessage request = Requisicao(metodo, rota, corpo);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, persona);
        if (papel is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        if (metodo != HttpMethod.Get)
        {
            ExigirChaveDeIdempotencia(request);
        }

        return request;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string rota, object? corpo)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        return request;
    }

    private static void ExigirChaveDeIdempotencia(HttpRequestMessage request) =>
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

    private sealed class RegistroQueFalha : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker fora"));
    }
}
