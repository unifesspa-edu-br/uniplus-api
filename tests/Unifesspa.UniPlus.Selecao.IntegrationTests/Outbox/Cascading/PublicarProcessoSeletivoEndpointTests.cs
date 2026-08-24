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

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

// Cenário fim-a-fim do fluxo de referência ADR-0005 (Story #759, T4 #785):
// HTTP request → PublicarProcessoSeletivoCommand → handler convention-based
// produtivo → ProcessoSeletivo.Publicar(, CorpusEnvelope.ContextoRico()) emite ProcessoPublicadoEvent via
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
        await AssertGateEstruturalNaoConformeAsync(
            (db, nome) => ProcessoSeletivoPendenciasSeeder.SemearComCadastroProprioNaoConformeAsync(db, nome),
            "uniplus.selecao.processo_seletivo.conformidade_insuficiente",
            "atendimento_especializado_ausente",
            nameof(Publicar_QuandoCadastroProprioNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com cronograma não conforme (InscricaoPropria sem fase de coleta) — 422 inclui pendencias (issue #1096 CA-01, cenário Gherkin da issue)")]
    public async Task Publicar_QuandoCronogramaNaoConforme_Retorna422ComPendencias() =>
        await AssertGateEstruturalNaoConformeAsync(
            ProcessoSeletivoPendenciasSeeder.SemearComCronogramaNaoConformeAsync,
            "uniplus.selecao.processo_seletivo.inscricao_propria_sem_fase_de_coleta",
            "cronograma_inscricao_propria_sem_fase_de_coleta",
            nameof(Publicar_QuandoCronogramaNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com cascata não conforme (fora do regime federal) — 422 inclui pendencias (issue #1096 CA-02)")]
    public async Task Publicar_QuandoCascataNaoConforme_Retorna422ComPendencias() =>
        await AssertGateEstruturalNaoConformeAsync(
            ProcessoSeletivoPendenciasSeeder.SemearComCascataNaoConformeAsync,
            "uniplus.selecao.processo_seletivo.cascata_fora_do_regime_federal",
            "cascata_modalidade_fora_do_regime_federal",
            nameof(Publicar_QuandoCascataNaoConforme_Retorna422ComPendencias));

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao com pré-canonicalização não conforme (CONDICIONAL vazia) — 422 inclui pendencias (issue #1096 CA-03)")]
    public async Task Publicar_QuandoPreCanonicalizacaoNaoConforme_Retorna422ComPendencias() =>
        await AssertGateEstruturalNaoConformeAsync(
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
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) =
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

    private static async Task<HttpResponseMessage> GetConformidadeAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/conformidade", UriKind.Relative));
        AppendTestAuth(request);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>
    /// Semeia via <paramref name="semear"/>, publica e confirma que o 422 tem o <paramref
    /// name="codigoEsperado"/> e que <c>pendencias</c> contém o item de código <paramref
    /// name="codigoDoItemVermelho"/> — corpo comum aos quatro grupos de gate estrutural da
    /// issue #1096 (CA-01 a CA-04).
    /// </summary>
    /// <remarks>
    /// A asserção é pelo código do item, não pela frase: a mensagem é redação e muda sem que
    /// a invariante mude, e um teste preso ao texto quebraria na primeira revisão editorial
    /// sem nada ter regredido.
    /// </remarks>
    private async Task AssertGateEstruturalNaoConformeAsync(
        Func<SelecaoDbContext, string, Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)>> semear,
        string codigoEsperado,
        string codigoDoItemVermelho,
        string nomeDoCenario)
    {
        ArgumentNullException.ThrowIfNull(semear);

        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await semear(db, $"{nomeDoCenario} {Guid.CreateVersion7()}");

        HttpResponseMessage response = await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be(codigoEsperado);
        doc.RootElement.GetProperty("pendencias").EnumerateArray()
            .Select(e => e.GetProperty("codigo").GetString())
            .Should().Contain(codigoDoItemVermelho);
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao recusado por obrigatoriedade legal com checklist estrutural verde — 422 com obrigatoriedadesReprovadas e SEM pendencias (issue #1096 CA-05/CA-07)")]
    public async Task Publicar_QuandoSoObrigatoriedadeLegalReprovada_Retorna422SemPendencias()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Guid regraId = await ProcessoSeletivoPendenciasSeeder.SemearObrigatoriedadeNaoAtendidaAsync(
            db, $"TEST_GATE_{Guid.CreateVersion7():N}");
        try
        {
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, nameof(Publicar_QuandoSoObrigatoriedadeLegalReprovada_Retorna422SemPendencias));

            HttpResponseMessage response = await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("code").GetString()
                .Should().Be("uniplus.selecao.processo_seletivo.conformidade_legal_insuficiente");
            doc.RootElement.GetProperty("obrigatoriedadesReprovadas").GetArrayLength().Should().BeGreaterThan(0);
            doc.RootElement.TryGetProperty("pendencias", out _).Should().BeFalse(
                "o checklist estrutural está 100% verde — só a obrigatoriedade legal reprovou");
        }
        finally
        {
            // A regra é universal (TipoProcessoUniversal) e sem vigenciaFim — sem esta limpeza,
            // ela continuaria reprovando todo processo SiSU publicado depois dela neste mesmo
            // container Postgres, compartilhado por toda a classe via CascadingFixture (afeta
            // até os testes que esperam 204, como Publicar_FluxoCompleto_DispatchaCascadingMessages).
            await db.Database.ExecuteSqlAsync($"UPDATE selecao.obrigatoriedades_legais SET is_deleted = true WHERE id = {regraId}");
        }
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao recusado por documento não confirmado, com checklist estrutural vermelho — 422 inclui pendencias mesmo sendo erro alheio à conformidade (issue #1096 CA-06)")]
    public async Task Publicar_QuandoOutroErro422EChecklistAindaVermelho_IncluiPendencias()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPendenciasSeeder
            .SemearComCadastroProprioNaoConformeAsync(
                db,
                nameof(Publicar_QuandoOutroErro422EChecklistAindaVermelho_IncluiPendencias),
                documentoConfirmado: false);

        HttpResponseMessage response = await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // A recusa é por documento não confirmado — nada a ver com conformidade estrutural —,
        // mas o checklist reconsultado no momento do enriquecimento ainda tem item vermelho, e
        // o contrato (issue #1096) diz que "pendencias" é um retrato do estado atual, não uma
        // tradução exclusiva do código de erro que efetivamente recusou.
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.documento_nao_confirmado");
        doc.RootElement.GetProperty("pendencias").EnumerateArray()
            .Select(e => e.GetProperty("codigo").GetString())
            .Should().Contain("atendimento_especializado_ausente");
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

        await TiposDeAtoSeeder.SemearAsync(api.Services);
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

    private static object NovoCorpoPublicacao(Guid documentoEditalId, string? numero = null) => new
    {
        numero,
        periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        documentoEditalId,
        ato = new
        {
            orgao = "CEPS",
            serie = "EDITAL",
            ano = 2026,
            dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assinante = "Diretor do CEPS",
            tipoAtoCodigo = "EDITAL_ABERTURA",
        },
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
