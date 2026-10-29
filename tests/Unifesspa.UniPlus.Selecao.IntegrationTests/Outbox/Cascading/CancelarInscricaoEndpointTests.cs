namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// <c>POST /processos-seletivos/{id}/inscricao/cancelamento</c> pelo HTTP: o corpo é opcional (o motivo
/// cai em "a pedido do candidato"), então o POST sem corpo tem de chegar ao handler em vez de morrer no
/// binder com 400.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class CancelarInscricaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public CancelarInscricaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "POST de cancelamento sem corpo passa pelo binder e chega ao handler (sem inscrição: 404, nunca 400)")]
    public async Task SemCorpo_ChegaAoHandler()
    {
        CascadingApiFactory api = _fixture.Factory;
        Guid processoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"Cancelamento sem corpo {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/inscricao/cancelamento", UriKind.Relative))
        {
            // Como o cliente do candidato envia: JSON declarado e corpo vazio.
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.CpfHeader, PersonasDeTeste.KevinCarvalho.Valor);
        request.Headers.TryAddWithoutValidation("If-Match", "\"qualquer\"");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString());

        using HttpClient client = api.CreateClient();
        HttpResponseMessage resposta = await client.SendAsync(request);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "o corpo é opcional: sem inscrição, a recusa é a do handler, e não a do binder");
    }
}
