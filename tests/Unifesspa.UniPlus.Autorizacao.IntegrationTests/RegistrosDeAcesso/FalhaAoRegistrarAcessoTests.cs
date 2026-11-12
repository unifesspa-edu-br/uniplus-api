namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.RegistrosDeAcesso;

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
/// Diogo Souza é gestor de acessos; Cecília Moura é Encarregada de dados, e cada leitura dela é recusada sem o registro.
/// </summary>
[Collection(AutorizacaoEndpointCollection.Name)]
public sealed class FalhaAoRegistrarAcessoTests
{
    private const string Falha = "uniplus.autorizacao.registro_de_acesso.falha_ao_gravar";

    private readonly AutorizacaoEndpointFixture _fixture;

    public FalhaAoRegistrarAcessoTests(AutorizacaoEndpointFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "Com o registro indisponível, a consulta protegida responde 503 e não entrega o dado")]
    public async Task Acesso_RegistroIndisponivel_503()
    {
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, "diogo.souza", TipoDeGovernanca.GestorDeAcessos);
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = ComRegistroQueFalha();
        using HttpClient diogo = Cliente(comFalha, "diogo.souza");

        using HttpResponseMessage resposta = await diogo.GetAsync(new Uri("/api/autorizacao/concessoes", UriKind.Relative));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        corpo.Should().Contain(Falha).And.NotContain("banco fora");
    }

    [Theory(DisplayName = "Com o registro indisponível, cada leitura do Encarregado de dados responde 503 sem entregar o dado")]
    [InlineData("/api/autorizacao/pedidos-de-concessao?fila=todos")]
    [InlineData("/api/autorizacao/concessoes")]
    [InlineData("/api/autorizacao/instalacoes/tentativas-sem-pendencia")]
    [InlineData("/api/autorizacao/revisoes")]
    [InlineData("/api/autorizacao/perfis")]
    [InlineData("/api/autorizacao/governanca/divergencias")]
    [InlineData("/api/configuracao/admin/protecao-do-catalogo")]
    [InlineData("/api/configuracao/admin/protecao-do-catalogo/decisoes")]
    public async Task LeituraDoEncarregado_RegistroIndisponivel_503(string rota)
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, "cecilia.moura");
        await ConcessoesDeTeste.ConcederGovernancaAsync(_fixture.Factory.Services, "cecilia.moura", TipoDeGovernanca.EncarregadoDeDados);
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = ComRegistroQueFalha();
        using HttpClient cecilia = Cliente(comFalha, "cecilia.moura");

        using HttpResponseMessage resposta = await cecilia.GetAsync(new Uri(rota, UriKind.Relative));

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, corpo);
        corpo.Should().Contain(Falha).And.NotContain("\"items\"");
    }

    [Fact(DisplayName = "Com o registro indisponível, o pedido inexistente pedido sem permissão responde 503, como o existente")]
    public async Task PedidoInexistente_RegistroIndisponivel_503()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, "paulo.nogueira");
        await using WebApplicationFactory<HostAssemblyMarker> comFalha = ComRegistroQueFalha();
        using HttpClient paulo = Cliente(comFalha, "paulo.nogueira");
        string pedido = $"/api/autorizacao/pedidos-de-concessao/{Guid.CreateVersion7()}";

        using HttpResponseMessage leitura = await paulo.GetAsync(new Uri(pedido, UriKind.Relative));
        using HttpResponseMessage anexo = await paulo.GetAsync(new Uri($"{pedido}/anexos/{Guid.CreateVersion7()}", UriKind.Relative));
        using HttpResponseMessage aprovacao = await Enviar(paulo, $"{pedido}/aprovacoes", new { });
        using HttpResponseMessage recusa = await Enviar(paulo, $"{pedido}/recusa", new { motivo = "Sem justificativa de acesso." });

        new[] { leitura, anexo, aprovacao, recusa }.Select(static r => r.StatusCode).Should().OnlyContain(static s => s == HttpStatusCode.ServiceUnavailable);
    }

    private WebApplicationFactory<HostAssemblyMarker> ComRegistroQueFalha() =>
        _fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRegistroOperacionalRestrito>();
                services.AddSingleton<IRegistroOperacionalRestrito, RegistroQueFalha>();
            }));

    private static HttpClient Cliente(WebApplicationFactory<HostAssemblyMarker> fabrica, string sub)
    {
        HttpClient client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.NameHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, "teste@unifesspa.edu.br");

        return client;
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
