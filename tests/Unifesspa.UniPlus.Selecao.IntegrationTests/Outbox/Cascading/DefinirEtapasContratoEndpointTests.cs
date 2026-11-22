namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PublicacaoPeloEndpointDeTeste;

[Collection(CascadingCollection.Name)]
public sealed class DefinirEtapasContratoEndpointTests(CascadingFixture fixture)
{
    [Theory]
    [InlineData("produtos")]
    [InlineData("bancas")]
    [InlineData("recursos")]
    public async Task Definir_ColecaoOmitida_400ComCampoEEnvelopeCanonico(string campo)
    {
        (HttpClient client, Guid processoId) = await PrepararAsync();
        JsonObject etapa = Etapa();
        etapa.Remove(campo).Should().BeTrue();
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, JsonContent.Create(new JsonArray(etapa)));
        JsonElement problema = await ExigirErroAsync(resposta, HttpStatusCode.BadRequest, "uniplus.requisicao.campo_obrigatorio_ausente");
        problema.GetRawText().Should().ContainEquivalentOf(campo);
    }

    [Fact]
    public async Task Definir_SemCorpo_400ComCodigoProprio()
    {
        (HttpClient client, Guid processoId) = await PrepararAsync();
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"));
        JsonElement problema = await ExigirErroAsync(resposta, HttpStatusCode.BadRequest, "uniplus.requisicao.corpo_ausente");
        problema.GetRawText().Should().NotContain("A non-empty request body is required");
    }

    [Theory]
    [InlineData("produtos")]
    [InlineData("bancas")]
    [InlineData("recursos")]
    public async Task Definir_ColecaoNula_422ComCampoNomeado(string campo)
    {
        (HttpClient client, Guid processoId) = await PrepararAsync();
        JsonObject etapa = Etapa();
        etapa[campo] = null;
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, JsonContent.Create(new JsonArray(etapa)));
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString())
            .Should().Contain($"etapas[0].{campo}");
    }

    [Fact]
    public async Task Definir_ProcessoComClassificacaoImportadaEAListaVazia_200ComListaVazia()
    {
        Guid processoId = await SemearProcessoSemEtapasAsync();
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, JsonContent.Create(new JsonArray()));

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task Definir_Publicado_422ComMutacaoBloqueada()
    {
        await TiposDeAtoSeeder.SemearAsync(fixture.Factory.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(fixture.Factory, nameof(Definir_Publicado_422ComMutacaoBloqueada));
        using HttpClient client = fixture.Factory.CreateClient();
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, JsonContent.Create(Array.Empty<object>()));
        await ExigirErroAsync(resposta, HttpStatusCode.UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_pos_publicacao_bloqueada");
    }

    [Fact]
    public async Task Definir_ChaveDeIdempotenciaComEspacos_400ComCodigoProprio()
    {
        (HttpClient client, Guid processoId) = await PrepararAsync();
        using HttpResponseMessage resposta = await EnviarAsync(client, processoId, JsonContent.Create(Array.Empty<object>()), "chave inválida com espaços");
        await ExigirErroAsync(resposta, HttpStatusCode.BadRequest, "uniplus.idempotency.key_malformada");
    }

    private async Task<Guid> SemearProcessoSemEtapasAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = ApoioDoProcessoConformeSemeado.ProcessoBaseConforme(db, "PSR 2027");
        await db.ProcessosSeletivos.AddAsync(processo);
        await db.SaveChangesAsync();
        return processo.Id;
    }

    private async Task<(HttpClient Client, Guid ProcessoId)> PrepararAsync()
    {
        (Guid processoId, _) = await SemearProcessoAsync(fixture.Factory, nameof(DefinirEtapasContratoEndpointTests));
        return (fixture.Factory.CreateClient(), processoId);
    }

    private static JsonObject Etapa() => new()
    {
        ["nome"] = "Prova Objetiva",
        ["carater"] = 3,
        ["tipoEtapaOrigemId"] = PayloadsDoEnvelopeFechado.TipoEtapaProvaObjetivaOrigemId,
        ["peso"] = 1,
        ["notaMinima"] = 10,
        ["ordem"] = 1,
        ["produtos"] = new JsonArray(),
        ["bancas"] = new JsonArray(),
        ["recursos"] = new JsonArray(),
    };

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, Guid processoId, HttpContent? content, string? chave = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", chave ?? Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ExigirErroAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(status, corpo);
        JsonElement problema = JsonSerializer.Deserialize<JsonElement>(corpo);
        problema.GetProperty("code").GetString().Should().Be(codigo);
        problema.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        problema.GetProperty("type").GetString().Should().EndWith(codigo);
        problema.GetProperty("status").GetInt32().Should().Be((int)status);
        corpo.Should().NotContain("Unifesspa.UniPlus").And.NotContain("System.");
        return problema;
    }
}
