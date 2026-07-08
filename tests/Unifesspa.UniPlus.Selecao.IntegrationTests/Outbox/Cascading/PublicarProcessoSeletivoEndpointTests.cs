namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim do fluxo de referência ADR-0005 (Story #759, T4 #785):
// HTTP request → PublicarProcessoSeletivoCommand → handler convention-based
// produtivo → ProcessoSeletivo.Publicar() emite ProcessoPublicadoEvent via
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
public sealed class PublicarProcessoSeletivoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public PublicarProcessoSeletivoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao dispara cascading e entrega ProcessoPublicadoEvent ao subscritor")]
    public async Task Publicar_FluxoCompleto_DispatchaCascadingMessages()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Publicar_FluxoCompleto_DispatchaCascadingMessages));

        HttpResponseMessage response = await PostPublicarAsync(client, processoId, documentoId, MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        ProcessoPublicadoEvent? evento = await CascadingScenariosTests.EsperarEventoAsync(
            collector, processoId, TimeSpan.FromSeconds(15));

        evento.Should().NotBeNull(
            "o handler produtivo retorna o evento via cascading; o listener PG entrega ao subscritor de teste");
        evento!.ProcessoSeletivoId.Should().Be(processoId);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ProcessoSeletivo? persistido = await db.ProcessosSeletivos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processoId);
        persistido.Should().NotBeNull();
        persistido!.Status.Should().Be(StatusProcesso.Publicado);
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao retorna 404 quando o processo não existe")]
    public async Task Publicar_QuandoProcessoNaoExiste_Retorna404()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Guid inexistente = Guid.CreateVersion7();

        HttpResponseMessage response = await PostPublicarAsync(client, inexistente, Guid.CreateVersion7(), MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.nao_encontrado");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com keys diferentes — segunda chamada retorna 422 TransicaoInvalida")]
    public async Task Publicar_QuandoJaPublicado_Retorna422()
    {
        // Cliente com keys distintas força handler a executar duas vezes;
        // segunda execução vê processo já publicado e retorna 422 (regra de
        // domínio, não idempotência do middleware Idempotency-Key).
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Publicar_QuandoJaPublicado_Retorna422));

        HttpResponseMessage primeira = await PostPublicarAsync(client, processoId, documentoId, MakeIdempotencyKey());
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage segunda = await PostPublicarAsync(client, processoId, documentoId, MakeIdempotencyKey());
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.transicao_invalida");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com mesma Idempotency-Key — replay verbatim com Idempotency-Replayed: true")]
    public async Task Publicar_MesmaKey_ReplayVerbatim()
    {
        CascadingApiFactory api = _fixture.Factory;
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
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/publicacao", UriKind.Relative))
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
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/publicacao", UriKind.Relative))
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

    private static object NovoCorpoPublicacao(Guid documentoEditalId, string? numero = null) => new
    {
        numero,
        periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        documentoEditalId,
    };

    private static async Task<HttpResponseMessage> PostPublicarAsync(
        HttpClient client, Guid processoId, Guid documentoEditalId, string idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoPublicacao(documentoEditalId)),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// Adiciona Authorization Bearer + role plataforma-admin ao request —
    /// exigida por [Authorize(Roles = "plataforma-admin")] no controller.
    /// </summary>
    private static void AppendTestAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");

        return (processo.Id, documento.Id);
    }
}
