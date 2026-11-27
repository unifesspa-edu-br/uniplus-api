namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Host;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O que as suítes HTTP do rascunho do processo seletivo repetem: o documento, o envio autenticado, a concessão, a criação
/// e a exportação. Com <see cref="HostComGeoSubstituido"/>, as requisições vão ao host que o teste criou por
/// <see cref="HostComGeo"/> e descarta; sem ele, ao host da fixture.
/// </summary>
internal sealed class ClienteDoRascunhoDeTeste(CascadingFixture fixture)
{
    public const string Rota = "/api/selecao/rascunhos-de-processos-seletivos";

    public WebApplicationFactory<HostAssemblyMarker>? HostComGeoSubstituido { get; init; }

    public static WebApplicationFactory<HostAssemblyMarker> HostComGeo(CascadingFixture ambiente, IConferenciaEnderecoGeo geo) =>
        ambiente.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConferenciaEnderecoGeo>();
            services.AddSingleton(geo);
        }));

    public static string Documento(string unidade, string nome = "Processo Seletivo de Exemplo") =>
        $$"""{ "formato": "uniplus.selecao.configuracao", "versaoFormato": 1, "identificacao": { "nome": "{{nome}}", "unidadeAdministradoraCodigo": "{{unidade}}" } }""";

    public static async Task<JsonElement> Corpo(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return documento.RootElement.Clone();
    }

    public static HttpRequestMessage Requisicao(string contaId, HttpMethod metodo, string rota, string? corpo = null, string? ifMatch = null, string? chave = null)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, contaId);
        request.Content = corpo is null ? null : new StringContent(corpo, Encoding.UTF8, "application/json");
        AdicionarCabecalho(request, "If-Match", ifMatch);
        AdicionarCabecalho(request, "Idempotency-Key", chave);
        return request;
    }

    public async Task<HttpResponseMessage> EnviarAsync(
        string contaId, HttpMethod metodo, string rota, string? corpo = null, string? ifMatch = null, string? chave = null)
    {
        using HttpClient client = (HostComGeoSubstituido ?? fixture.Factory).CreateClient();
        using HttpRequestMessage request = Requisicao(contaId, metodo, rota, corpo, ifMatch, chave);

        return await client.SendAsync(request);
    }

    public async Task ConcederAsync(string contaId, Guid unidade)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(fixture.Factory.Services, contaId, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    public async Task<Guid> CriarAsync(string contaId, string unidade)
    {
        using HttpResponseMessage criado = await EnviarAsync(contaId, HttpMethod.Post, Rota, Documento(unidade), chave: Guid.CreateVersion7().ToString());
        criado.StatusCode.Should().Be(HttpStatusCode.Created);
        criado.Headers.ETag!.Tag.Should().Be("\"1\"");
        Guid id = (await Corpo(criado)).GetProperty("id").GetGuid();
        criado.Headers.Location!.ToString().Should().EndWith(id.ToString());
        return id;
    }

    public async Task<Guid> CriarComDocumentoAsync(string contaId, string documento)
    {
        using HttpResponseMessage resposta = await EnviarAsync(contaId, HttpMethod.Post, Rota, documento, chave: Guid.CreateVersion7().ToString());
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        return (await Corpo(resposta)).GetProperty("id").GetGuid();
    }

    public async Task<JsonObject> ExportarAsync(string contaId, Guid id)
    {
        using HttpResponseMessage resposta = await EnviarAsync(contaId, HttpMethod.Get, $"{Rota}/{id}");
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!.AsObject();
    }

    public async Task<JsonNode> DocumentoGuardadoAsync(Guid id)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string documento = await banco.RascunhosDeProcessosSeletivos.AsNoTracking().Where(r => r.Id == id).Select(r => r.Documento).SingleAsync();
        return JsonNode.Parse(documento)!;
    }

    private static void AdicionarCabecalho(HttpRequestMessage request, string nome, string? valor)
    {
        if (valor is not null)
        {
            request.Headers.TryAddWithoutValidation(nome, valor);
        }
    }
}
