namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O <c>PUT /processos-seletivos/{id}/etapas</c> com o nome da etapa omitido, nulo ou em branco: 422 apontando o nome,
/// na mesma resposta do período invertido da mesma etapa.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class NomeDaEtapaOmitidoEndpointTests
{
    private const string NomeObrigatorio = "uniplus.selecao.etapa_processo.nome_obrigatorio";
    private const string PeriodoInvertido = "uniplus.selecao.etapa_processo.janela_invertida";

    private readonly CascadingFixture _fixture;

    public NomeDaEtapaOmitidoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "A etapa com o nome ausente, nulo ou em branco é recusada com 422 apontando o nome")]
    [InlineData("ausente")]
    [InlineData("nulo")]
    [InlineData("em branco")]
    public async Task EtapaSemNome_RecusaApontandoONome(string situacaoDoNome)
    {
        CascadingApiFactory api = _fixture.Factory;
        Guid processoId = await SemearRascunhoAsync(api);
        JsonObject etapa = EtapaValida();
        DefinirNome(etapa, situacaoDoNome);

        using HttpResponseMessage resposta = await PutEtapasAsync(api, processoId, new JsonArray(etapa));

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(resposta, ("etapas[0].nome", NomeObrigatorio));
    }

    [Fact(DisplayName = "O nome ausente e o período invertido da etapa vêm na mesma resposta de 422")]
    public async Task EtapaSemNomeEComPeriodoInvertido_RecusaAsDuas()
    {
        CascadingApiFactory api = _fixture.Factory;
        Guid processoId = await SemearRascunhoAsync(api);
        JsonObject etapa = EtapaValida();
        etapa.Remove("nome");
        etapa["inicio"] = "2026-02-01T00:00:00Z";
        etapa["fim"] = "2026-01-01T00:00:00Z";

        using HttpResponseMessage resposta = await PutEtapasAsync(api, processoId, new JsonArray(etapa));

        await RecusaDeCampoObrigatorioNoDominio.ConferirRecusasAsync(
            resposta, ("etapas[0].nome", NomeObrigatorio), ("etapas[0].fim", PeriodoInvertido));
    }

    private static void DefinirNome(JsonObject etapa, string situacaoDoNome)
    {
        switch (situacaoDoNome)
        {
            case "ausente":
                etapa.Remove("nome");
                break;
            case "nulo":
                etapa["nome"] = null;
                break;
            default:
                etapa["nome"] = "   ";
                break;
        }
    }

    private static JsonObject EtapaValida() => new()
    {
        ["nome"] = "Prova Objetiva",
        ["carater"] = "CLASSIFICATORIA",
        ["tipoEtapaOrigemId"] = Guid.CreateVersion7(),
        ["peso"] = 1,
        ["ordem"] = 1,
        ["produtos"] = new JsonArray(),
        ["bancas"] = new JsonArray(),
        ["recursos"] = new JsonArray(),
    };

    private static async Task<HttpResponseMessage> PutEtapasAsync(CascadingApiFactory api, Guid processoId, JsonArray corpo)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/etapas", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await api.CreateClient().SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<Guid> SemearRascunhoAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (Domain.Entities.ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Etapa sem nome {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
