namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using System.Net.Http.Headers;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Kernel.Results;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim do fluxo de referência ADR-0005: HTTP request →
// PublicarEditalCommand → handler convention-based produtivo → Edital.Publicar()
// emite EditalPublicadoEvent via AddDomainEvent → handler retorna
// (Result, IEnumerable<object>) com o evento drenado por
// DequeueDomainEvents().Cast<object>() → CaptureCascadingMessages persiste
// envelope na MESMA transação do SaveChanges → listener da queue PG entrega
// ao subscritor (EditalPublicadoSubscriberHandler do teste, que registra no
// coletor; o EditalPublicadoEventHandler produtivo também é invocado pela
// fan-out, executa logging estruturado e não interfere no estado do coletor).
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class PublicarEditalEndpointTests
{
    // Contador atômico para numeroSeed: garante unicidade dentro do test run
    // (CascadingFixture é singleton da collection com PG efêmero, então o
    // contador zera junto com o banco). Substituiu Math.Abs(Guid.NewGuid().GetHashCode() % 9000)
    // que tinha ~0,01% de chance de colidir com valores hard-coded em outros testes.
    // Range 1..9999 (NumeroEdital aceita 1..9999); módulo 9999 + 1 evita zero.
    private static int _numeroSeedCounter;

    private static int NextNumeroSeed() =>
        (Interlocked.Increment(ref _numeroSeedCounter) % 9999) + 1;

    private readonly CascadingFixture _fixture;

    public PublicarEditalEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar dispara cascading e entrega EditalPublicadoEvent ao subscritor")]
    public async Task PublicarEdital_FluxoCompleto_DispatchaCascadingMessages()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();
        collector.Clear();

        Edital edital = await SemearEditalAsync(api);

        HttpResponseMessage response = await PostPublicarAsync(client, edital.Id, MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        EditalPublicadoEvent? evento = await CascadingScenariosTests.EsperarEventoAsync(
            collector,
            edital.NumeroEdital,
            TimeSpan.FromSeconds(15));

        evento.Should().NotBeNull(
            "o handler produtivo retorna o evento via cascading; o listener PG entrega ao subscritor de teste");
        evento!.EditalId.Should().Be(edital.Id);
        evento.NumeroEdital.Should().Be(edital.NumeroEdital.ToString());

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Edital? persistido = await db.Editais.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == edital.Id);
        persistido.Should().NotBeNull();
        persistido!.Status.Should().Be(StatusEdital.Publicado);
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar retorna 404 quando o edital não existe")]
    public async Task PublicarEdital_QuandoEditalNaoExiste_Retorna404()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Guid inexistente = Guid.CreateVersion7();

        HttpResponseMessage response = await PostPublicarAsync(client, inexistente, MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.edital.nao_encontrado");
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar com keys diferentes — segunda chamada retorna 422 Edital.JaPublicado")]
    public async Task PublicarEdital_QuandoJaPublicado_Retorna422()
    {
        // Cliente com keys distintas força handler a executar duas vezes;
        // segunda execução vê edital já publicado e retorna 422 (idempotência
        // semântica do domínio, não do middleware Idempotency-Key).
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Edital edital = await SemearEditalAsync(api);

        HttpResponseMessage primeira = await PostPublicarAsync(client, edital.Id, MakeIdempotencyKey());
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage segunda = await PostPublicarAsync(client, edital.Id, MakeIdempotencyKey());
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.edital.ja_publicado");
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar com mesma Idempotency-Key — replay verbatim com Idempotency-Replayed: true")]
    public async Task PublicarEdital_MesmaKey_ReplayVerbatim()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        Edital edital = await SemearEditalAsync(api);
        string key = MakeIdempotencyKey();

        HttpResponseMessage primeira = await PostPublicarAsync(client, edital.Id, key);
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);
        primeira.Headers.Contains("Idempotency-Replayed").Should().BeFalse();

        HttpResponseMessage segunda = await PostPublicarAsync(client, edital.Id, key);
        segunda.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "mesma key + body vazio idêntico → handler NÃO roda; cache replay verbatim");
        segunda.Headers.Contains("Idempotency-Replayed").Should().BeTrue();
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar sem Idempotency-Key retorna 400 uniplus.idempotency.key_ausente")]
    public async Task PublicarEdital_SemKey_Retorna400()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            new Uri($"/api/editais/{Guid.CreateVersion7()}/publicar", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.key_ausente");
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar com Idempotency-Key malformada retorna 400 uniplus.idempotency.key_malformada")]
    public async Task PublicarEdital_KeyMalformada_Retorna400()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/editais/{Guid.CreateVersion7()}/publicar", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", "invalid key with spaces");

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.key_malformada");
    }

    [Fact(DisplayName =
        "POST /editais/{id}/publicar anonymous com Idempotency-Key retorna 401 uniplus.idempotency.principal_requerido")]
    public async Task PublicarEdital_Anonymous_Retorna401()
    {
        // Endpoint marcado com [RequiresIdempotencyKey] exige principal —
        // sem auth, filter rejeita para evitar cache poisoning entre clientes.
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/editais/{Guid.CreateVersion7()}/publicar", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
        // Sem AppendTestAuth — request anonymous deliberada.

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.principal_requerido");
    }

    [Fact(DisplayName =
        "POST /editais com mesma Idempotency-Key e body diferente retorna 422 uniplus.idempotency.body_mismatch")]
    public async Task CriarEdital_MesmaKeyBodyDiferente_Retorna422BodyMismatch()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        string key = MakeIdempotencyKey();
        int numero1 = NextNumeroSeed();
        int numero2 = NextNumeroSeed(); // body diferente garantido (incrementos sequenciais)

        // Primeira request: cria edital normalmente.
        using HttpRequestMessage primeiraReq = new(HttpMethod.Post, new Uri("/api/editais", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numeroEdital = numero1,
                anoEdital = 2026,
                titulo = "Body-mismatch test",
                tipoProcesso = 1, // SiSU — enum serializado como número (System.Text.Json default)
            }),
        };
        AppendTestAuth(primeiraReq);
        primeiraReq.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        HttpResponseMessage primeira = await client.SendAsync(primeiraReq);
        primeira.StatusCode.Should().Be(HttpStatusCode.Created);

        // Segunda request: mesma key, body diferente (numero2).
        using HttpRequestMessage segundaReq = new(HttpMethod.Post, new Uri("/api/editais", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numeroEdital = numero2,
                anoEdital = 2026,
                titulo = "Body-mismatch test",
                tipoProcesso = 1, // SiSU — enum serializado como número (System.Text.Json default)
            }),
        };
        AppendTestAuth(segundaReq);
        segundaReq.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        HttpResponseMessage segunda = await client.SendAsync(segundaReq);

        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.idempotency.body_mismatch");
    }

    private static async Task<HttpResponseMessage> PostPublicarAsync(HttpClient client, Guid editalId, string idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/editais/{editalId}/publicar", UriKind.Relative));
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// Adiciona Authorization Bearer + X-Test-User-Id ao request. Necessário
    /// porque [RequiresIdempotencyKey] exige principal autenticado (filter
    /// rejeita anonymous para evitar cache poisoning entre clientes).
    /// </summary>
    private static void AppendTestAuth(HttpRequestMessage request, string userId = "test-publicar-user")
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, userId);
    }

    private static async Task<Edital> SemearEditalAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();

        // Numero estável via contador atômico — Edital.Numero é int (1..9999),
        // colisão com testes paralelos é evitada pelo CollectionFixture (1
        // fixture por collection, fixture é singleton dentro do test run).
        int numeroSeed = NextNumeroSeed();
        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(numero: numeroSeed, ano: 2026);
        numeroResult.IsSuccess.Should().BeTrue();
        Edital edital = Edital.Criar(numeroResult.Value!, "PublicarEditalEndpointTests seed", TipoProcesso.SiSU);
        // O agregado já tem domain events na coleção (do `Criar`/Publicar futuro).
        // Como esta seed bypassa o handler produtivo, drenamos manualmente para
        // não vazar eventos no coletor antes do POST.
        edital.ClearDomainEvents();
        await db.Editais.AddAsync(edital);
        await db.SaveChangesAsync();

        return edital;
    }
}
