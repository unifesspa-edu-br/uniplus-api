namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.RegistrosDeAcesso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Com o registro de acesso indisponível, a operação protegida responde 503 com o código próprio e o dado não é entregue.
/// Diogo Souza é gestor de acessos.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class FalhaAoRegistrarAcessoTests
{
    private readonly AutorizacaoEndpointFixture _fixture;

    public FalhaAoRegistrarAcessoTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Com o registro indisponível, a consulta protegida responde 503 e não entrega o dado")]
    public async Task Acesso_RegistroIndisponivel_503()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, "diogo.souza", TipoDeGovernanca.GestorDeAcessos);
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));
        using HttpClient diogo = comFalha.CreateClient();
        diogo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        diogo.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "diogo.souza");
        diogo.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "diogo.souza");
        diogo.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        corpo.Should().Contain("uniplus.autorizacao.registro_de_acesso.falha_ao_gravar").And.NotContain("banco fora");
    }

    [Fact(DisplayName = "Com o registro indisponível, o pedido inexistente pedido sem permissão responde 503, como o existente")]
    public async Task PedidoInexistente_RegistroIndisponivel_503()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, "paulo.nogueira");
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));
        using HttpClient paulo = comFalha.CreateClient();
        paulo.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        paulo.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "paulo.nogueira");
        paulo.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, "paulo.nogueira");
        paulo.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");
        string pedido = $"/api/autorizacao/pedidos-de-concessao/{Guid.CreateVersion7()}";

        using HttpResponseMessage leitura = await paulo.GetAsync(new Uri(pedido, UriKind.Relative));
        using HttpResponseMessage anexo = await paulo.GetAsync(new Uri($"{pedido}/anexos/{Guid.CreateVersion7()}", UriKind.Relative));
        using HttpResponseMessage aprovacao = await Enviar(paulo, $"{pedido}/aprovacoes", new { });
        using HttpResponseMessage recusa = await Enviar(paulo, $"{pedido}/recusa", new { motivo = "Sem justificativa de acesso." });

        foreach (HttpResponseMessage resposta in new[] { leitura, anexo, aprovacao, recusa })
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, await resposta.Content.ReadAsStringAsync());
        }
    }

    private static async Task<HttpResponseMessage> Enviar(HttpClient client, string rota, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(rota, UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(corpo),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        return await client.SendAsync(request);
    }

    private sealed class RegistroQueFalha : IRegistroOperacionalRestrito
    {
        public Task RegistrarAsync(IReadOnlyList<RegistroDecisaoAcesso> registros, CancellationToken cancellationToken) =>
            throw new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("banco fora"));
    }
}
