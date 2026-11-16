namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.Events;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ApoioDaPublicacaoDoProcessoSeletivo;

// Cenário fim-a-fim do fluxo de referência ADR-0005 (Story #759, T4 #785):
// HTTP request → PublicarProcessoSeletivoCommand → handler convention-based
// produtivo → ProcessoSeletivo.Publicar(, CorpusEnvelopeDadosDoEdital.ContextoRico()) emite ProcessoPublicadoEvent via
// AddDomainEvent → handler retorna (Result, IEnumerable<object>) com o
// evento drenado por DequeueDomainEvents().Cast<object>() →
// CaptureCascadingMessages persiste envelope na MESMA transação do
// SaveChanges → listener da queue PG entrega ao subscritor
// (ProcessoPublicadoSubscriberHandler do teste, que registra no coletor; o
// ProcessoPublicadoEventHandler produtivo também é invocado pela fan-out,
// executa logging estruturado e não interfere no estado do coletor).

[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PublicarProcessoSeletivoIdempotenciaEndpointTests
{
    private readonly CascadingFixture _fixture;
    private readonly ApoioDaPublicacaoDoProcessoSeletivo _apoio;

    public PublicarProcessoSeletivoIdempotenciaEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPublicacaoDoProcessoSeletivo(fixture);
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com mesma Idempotency-Key — replay verbatim com Idempotency-Replayed: true")]
    public async Task Publicar_MesmaKey_ReplayVerbatim()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Publicar_MesmaKey_ReplayVerbatim));
        string key = MakeIdempotencyKey();

        HttpResponseMessage primeira = await PostPublicarAsync(client, processoId, documentoId, key);
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);
        primeira.Headers.Contains("Idempotency-Replayed").Should().BeFalse();

        HttpResponseMessage segunda = await PostPublicarAsync(client, processoId, documentoId, key);
        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "mesma key + mesmo body → handler NÃO roda; cache replay verbatim");
        segunda.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao sem Idempotency-Key retorna 400 uniplus.idempotency.key_ausente")]
    public async Task Publicar_SemKey_Retorna400()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, _) = await SemearAsync(api, nameof(Publicar_SemKey_Retorna400));
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(Guid.CreateVersion7())),
        };
        AppendTestAuth(request);

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.key_ausente");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com Idempotency-Key malformada retorna 400 uniplus.idempotency.key_malformada")]
    public async Task Publicar_KeyMalformada_Retorna400()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, _) = await SemearAsync(api, nameof(Publicar_KeyMalformada_Retorna400));
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(Guid.CreateVersion7())),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "invalid key with spaces");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.key_malformada");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao anonymous com Idempotency-Key retorna 401")]
    public async Task Publicar_Anonymous_Retorna401()
    {
        // Endpoint marcado com [RequiresIdempotencyKey] exige principal —
        // sem auth, filter rejeita para evitar cache poisoning entre clientes.
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(Guid.CreateVersion7())),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        // Sem AppendTestAuth — request anonymous deliberada.

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com mesma Idempotency-Key e body diferente retorna 422 uniplus.idempotency.body_mismatch")]
    public async Task Publicar_MesmaKeyBodyDiferente_Retorna422BodyMismatch()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Publicar_MesmaKeyBodyDiferente_Retorna422BodyMismatch));
        string key = MakeIdempotencyKey();

        using HttpRequestMessage primeiraReq = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(documentoId, numero: "001/2026")),
        };
        AppendTestAuth(primeiraReq);
        primeiraReq.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        HttpResponseMessage primeira = await client.SendAsync(primeiraReq);
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpRequestMessage segundaReq = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(documentoId, numero: "002/2026")),
        };
        AppendTestAuth(segundaReq);
        segundaReq.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        HttpResponseMessage segunda = await client.SendAsync(segundaReq);

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.body_mismatch");
    }
}
