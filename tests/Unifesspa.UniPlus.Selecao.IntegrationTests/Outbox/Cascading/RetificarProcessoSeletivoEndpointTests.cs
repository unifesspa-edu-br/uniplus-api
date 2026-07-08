namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim da retificação (Story #759, T5 #786, ADR-0101): publica
// via HTTP, depois retifica o Edital vigente — novo Edital de retificação +
// novo snapshot, cascading do ProcessoPublicadoEvent (reusado; a retificação
// é, em forma, outra emissão de Edital+snapshot). Reusa a mesma infra de
// cascading do fluxo de publicação.
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class RetificarProcessoSeletivoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public RetificarProcessoSeletivoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes emite Edital de retificação + snapshot e dispara cascading")]
    public async Task Retificar_FluxoCompleto_EmiteRetificacaoEDispatchaCascading()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        DomainEventCollector collector = api.Services.GetRequiredService<DomainEventCollector>();

        (Guid processoId, Guid documentoAbertura) = await SemearAsync(api, nameof(Retificar_FluxoCompleto_EmiteRetificacaoEDispatchaCascading));

        // Publica o Edital de abertura.
        HttpResponseMessage publicar = await PostPublicarAsync(client, processoId, documentoAbertura, MakeIdempotencyKey());
        publicar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid editalAberturaId = await ObterEditalUnicoAsync(api, processoId);
        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);

        // Retifica: o servidor infere o Edital vigente (a abertura) — o cliente
        // não informa id de Edital interno, só endereça o processo.
        HttpResponseMessage retificar = await PostRetificarAsync(
            client, processoId, documentoRetificacao, "Correção do prazo de inscrição", MakeIdempotencyKey());
        retificar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Lê o Edital de retificação persistido e espera o evento que carrega
        // ESTE editalId — a entrega cascading é assíncrona e o coletor também
        // guarda o evento da abertura (mesmo processoId); filtrar por editalId
        // evita a corrida de pegar o evento errado.
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        List<Edital> editais = await db.Set<Edital>().AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId).ToListAsync();
        editais.Should().HaveCount(2);
        Edital retificacao = editais.Single(e => e.Natureza == NaturezaEdital.Retificacao);
        retificacao.EditalRetificadoId.Should().Be(editalAberturaId);
        retificacao.MotivoRetificacao.Should().Be("Correção do prazo de inscrição");

        ProcessoPublicadoEvent? evento = await EsperarEventoPorEditalAsync(
            collector, retificacao.Id, TimeSpan.FromSeconds(15));
        evento.Should().NotBeNull("a retificação drena ProcessoPublicadoEvent pelo mesmo caminho cascading");
        evento!.ProcessoSeletivoId.Should().Be(processoId);
    }

    [Fact(DisplayName =
        "Retificação com motivo Unicode decomposto congela o mesmo valor NFC no snapshot e na coluna do Edital")]
    public async Task Retificar_MotivoUnicodeDecomposto_SnapshotEEditalReconciliam()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoAbertura) = await SemearAsync(api, nameof(Retificar_MotivoUnicodeDecomposto_SnapshotEEditalReconciliam));
        HttpResponseMessage publicar = await PostPublicarAsync(client, processoId, documentoAbertura, MakeIdempotencyKey());
        publicar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid documentoRetificacao = await SemearDocumentoConfirmadoAsync(api, processoId);

        // Motivo em forma DECOMPOSTA (NFD): ç = c + U+0327, ã = a + U+0303. O
        // handler deve normalizar para NFC uma vez e congelar o mesmo valor nos
        // dois lados (coluna do Edital e bloco 'retificacao' do snapshot).
        string motivoDecomposto = "correção do prazo de inscrição".Normalize(NormalizationForm.FormD);
        HttpResponseMessage retificar = await PostRetificarAsync(
            client, processoId, documentoRetificacao, motivoDecomposto, MakeIdempotencyKey());
        retificar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Edital retificacao = await db.Set<Edital>().AsNoTracking()
            .SingleAsync(e => e.ProcessoSeletivoId == processoId && e.Natureza == NaturezaEdital.Retificacao);
        SnapshotPublicacao snapshot = await db.SnapshotsPublicacao.AsNoTracking()
            .SingleAsync(s => s.EditalId == retificacao.Id);
        string motivoNoSnapshot = JsonNode.Parse(snapshot.ConfiguracaoCongelada)!
            .AsObject()["retificacao"]!["motivo"]!.GetValue<string>();

        // A coluna do Edital e o bloco congelado guardam o MESMO valor NFC — a
        // reconciliação do snapshot forense contra a linha do Edital vale mesmo
        // com input decomposto (Postgres não normaliza texto).
        retificacao.MotivoRetificacao.Should().Be(motivoNoSnapshot);
        retificacao.MotivoRetificacao.Should().Be(
            "correção do prazo de inscrição".Normalize(NormalizationForm.FormC));
    }

    private static async Task<ProcessoPublicadoEvent?> EsperarEventoPorEditalAsync(
        DomainEventCollector collector, Guid editalIdEsperado, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ProcessoPublicadoEvent? candidato = collector.Snapshot()
                .FirstOrDefault(e => e.EditalId == editalIdEsperado);
            if (candidato is not null)
            {
                return candidato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150));
        }

        return null;
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes em processo ainda em rascunho retorna 422 TransicaoInvalida (CA-09)")]
    public async Task Retificar_ProcessoRascunho_Retorna422()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Retificar_ProcessoRascunho_Retorna422));

        HttpResponseMessage response = await PostRetificarAsync(
            client, processoId, documentoId, "motivo", MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.transicao_invalida");
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/retificacoes em processo inexistente retorna 404")]
    public async Task Retificar_ProcessoInexistente_Retorna404()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await PostRetificarAsync(
            client, Guid.CreateVersion7(), Guid.CreateVersion7(), "motivo", MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.nao_encontrado");
    }

    private static object NovoCorpoPublicacao(Guid documentoEditalId) => new
    {
        numero = "001/2026",
        periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        documentoEditalId,
    };

    private static object NovoCorpoRetificacao(Guid documentoEditalId, string motivo) => new
    {
        motivo,
        numero = "001/2026-R1",
        periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

    private static async Task<HttpResponseMessage> PostRetificarAsync(
        HttpClient client, Guid processoId, Guid documentoEditalId, string motivo, string idempotencyKey)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/retificacoes", UriKind.Relative))
        {
            Content = JsonContent.Create(NovoCorpoRetificacao(documentoEditalId, motivo)),
        };
        AppendTestAuth(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");

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

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        string hashFixo = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processoId, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(2048, hashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosEdital.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    private static async Task<Guid> ObterEditalUnicoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.Set<Edital>().AsNoTracking()
            .Where(e => e.ProcessoSeletivoId == processoId)
            .Select(e => e.Id)
            .SingleAsync();
    }
}
