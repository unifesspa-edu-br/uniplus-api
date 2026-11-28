namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Assertions;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O <c>PUT /processos-seletivos/{id}/etapas</c> com uma etapa que omite as listas que o contrato exige: a resposta é a
/// do campo obrigatório ausente, 422, com um item em <c>errors</c> por campo, no ponteiro do item da lista.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CampoOmitidoDeItemDeListaEndpointTests
{
    private readonly CascadingFixture _fixture;

    public CampoOmitidoDeItemDeListaEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "A etapa sem produtos, bancas e recursos é recusada com 422, o código de campo obrigatório e um item por campo no ponteiro da etapa")]
    public async Task EtapaSemAsListasObrigatorias_RecusaNomeandoCadaCampo()
    {
        CascadingApiFactory api = _fixture.Factory;
        Guid processoId = await SemearRascunhoAsync(api);

        using HttpResponseMessage resposta = await PutEtapasAsync(api, processoId, new object[]
        {
            new { nome = "Prova Objetiva", tipoEtapaOrigemId = PayloadsDoEnvelopeFechado.TipoEtapaProvaObjetivaOrigemId, faseCodigo = "PROVA" },
        });

        await RecusaDeCampoObrigatorioNoDominio.ConferirCamposAsync(resposta, "etapas[0].produtos", "etapas[0].bancas", "etapas[0].recursos");
    }

    private static async Task<HttpResponseMessage> PutEtapasAsync(CascadingApiFactory api, Guid processoId, object corpo)
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
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Etapa sem listas {Guid.CreateVersion7()}" });
        return processo.Id;
    }
}
