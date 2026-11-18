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
public sealed class PublicarProcessoSeletivoEndpointTests
{
    private readonly CascadingFixture _fixture;
    private readonly ApoioDaPublicacaoDoProcessoSeletivo _apoio;

    public PublicarProcessoSeletivoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPublicacaoDoProcessoSeletivo(fixture);
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao dispara cascading e entrega ProcessoPublicadoEvent ao subscritor")]
    public async Task Publicar_FluxoCompleto_DispatchaCascadingMessages()
    {
        CascadingApiFactory api = _fixture.Factory;

        await TiposDeAtoSeeder.SemearAsync(api.Services);
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
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
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

        await TiposDeAtoSeeder.SemearAsync(api.Services);
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

        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        (Guid processoId, Guid documentoId) = await SemearAsync(api, nameof(Publicar_QuandoJaPublicado_Retorna422));

        HttpResponseMessage primeira = await PostPublicarAsync(client, processoId, documentoId, MakeIdempotencyKey());
        primeira.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage segunda = await PostPublicarAsync(client, processoId, documentoId, MakeIdempotencyKey());
        segunda.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.transicao_invalida");
        // issue #1096 CA-07: processo já publicado tem o checklist estrutural 100% verde —
        // "pendencias" nunca aparece vazio, e aqui nem deveria aparecer.
        doc.RootElement.TryGetProperty("pendencias", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com cadastro próprio não conforme — 422 ConformidadeInsuficiente inclui pendencias (issue #1096 CA-04, regressão)")]
    public async Task Publicar_QuandoCadastroProprioNaoConforme_Retorna422ComPendencias() =>
        await _apoio.AssertGateEstruturalNaoConformeAsync(
            (db, nome) => ProcessoSeletivoPendenciasSeeder.SemearComCadastroProprioNaoConformeAsync(db, nome),
            "uniplus.selecao.processo_seletivo.conformidade_insuficiente",
            "atendimento_especializado_ausente",
            nameof(Publicar_QuandoCadastroProprioNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com cronograma não conforme (InscricaoPropria sem fase de coleta) — 422 inclui pendencias (issue #1096 CA-01, cenário Gherkin da issue)")]
    public async Task Publicar_QuandoCronogramaNaoConforme_Retorna422ComPendencias() =>
        await _apoio.AssertGateEstruturalNaoConformeAsync(
            ProcessoSeletivoPendenciasSeeder.SemearComCronogramaNaoConformeAsync,
            "uniplus.selecao.processo_seletivo.inscricao_propria_sem_fase_de_coleta",
            "cronograma_inscricao_propria_sem_fase_de_coleta",
            nameof(Publicar_QuandoCronogramaNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com fase sem unidade responsável — 422 inclui pendencias e cita a fase")]
    public async Task Publicar_QuandoFaseSemUnidadeResponsavel_Retorna422ComPendencias() =>
        await _apoio.AssertGateEstruturalNaoConformeAsync(
            ProcessoSeletivoPendenciasSeeder.SemearComFaseSemUnidadeResponsavelAsync,
            "uniplus.selecao.processo_seletivo.fase_sem_unidade_responsavel",
            "cronograma_fase_sem_unidade_responsavel",
            nameof(Publicar_QuandoFaseSemUnidadeResponsavel_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com cascata não conforme (fora do regime federal) — 422 inclui pendencias (issue #1096 CA-02)")]
    public async Task Publicar_QuandoCascataNaoConforme_Retorna422ComPendencias() =>
        await _apoio.AssertGateEstruturalNaoConformeAsync(
            ProcessoSeletivoPendenciasSeeder.SemearComCascataNaoConformeAsync,
            "uniplus.selecao.processo_seletivo.cascata_fora_do_regime_federal",
            "cascata_modalidade_fora_do_regime_federal",
            nameof(Publicar_QuandoCascataNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com pré-canonicalização não conforme (CONDICIONAL vazia) — 422 inclui pendencias (issue #1096 CA-03)")]
    public async Task Publicar_QuandoPreCanonicalizacaoNaoConforme_Retorna422ComPendencias() =>
        await _apoio.AssertGateEstruturalNaoConformeAsync(
            ProcessoSeletivoPendenciasSeeder.SemearComPreCanonicalizacaoNaoConformeAsync,
            "uniplus.selecao.documento_exigido.condicional_vazia_determina_resultado",
            "exigencia_condicional_vazia_determina_resultado",
            nameof(Publicar_QuandoPreCanonicalizacaoNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "As pendências do 422 da publicação têm a MESMA identidade que o checklist de GET /conformidade — preview obsoleto e recusa são comparáveis por código e dimensão")]
    public async Task Publicar_Recusado_PendenciasCasamComOChecklistDoPreview()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) =
            await ProcessoSeletivoPendenciasSeeder.SemearComCronogramaNaoConformeAsync(
                db, $"{nameof(Publicar_Recusado_PendenciasCasamComOChecklistDoPreview)} {Guid.CreateVersion7()}");

        // O preview: o que o editor viu antes de tentar publicar.
        HttpResponseMessage preview = await GetConformidadeAsync(client, processo.Id);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument checklist = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());

        (string Codigo, string Dimensao)[] reprovadosNoPreview =
        [
            .. checklist.RootElement.GetProperty("itens").EnumerateArray()
                .Where(i => !i.GetProperty("ok").GetBoolean())
                .Select(i => (i.GetProperty("codigo").GetString()!, i.GetProperty("dimensao").GetString()!)),
        ];
        reprovadosNoPreview.Should().NotBeEmpty("pré-condição: o cenário semeado tem pendência estrutural");

        // A recusa: o que a publicação devolveu.
        HttpResponseMessage recusa = await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());
        recusa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await recusa.Content.ReadAsStringAsync());

        (string Codigo, string Dimensao)[] naRecusa =
        [
            .. problema.RootElement.GetProperty("pendencias").EnumerateArray()
                .Select(p => (p.GetProperty("codigo").GetString()!, p.GetProperty("dimensao").GetString()!)),
        ];

        naRecusa.Should().BeEquivalentTo(reprovadosNoPreview,
            "é a correspondência que permite ao cliente descobrir que o preview aprovado ficou obsoleto e levar "
            + "quem publica à mesma seção do editor, sem comparar frases");

        // A mensagem viaja junto, mas como texto humano — não é ela que identifica o item.
        problema.RootElement.GetProperty("pendencias").EnumerateArray()
            .Should().AllSatisfy(p => p.GetProperty("mensagem").GetString().Should().NotBeNullOrWhiteSpace());
    }
}
