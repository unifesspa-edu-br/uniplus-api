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
// produtivo → ProcessoSeletivo.Publicar(, ExemploDeEnvelopeDadosDoEdital.ContextoRico()) emite ProcessoPublicadoEvent via
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
public sealed class PublicarProcessoSeletivoRecusasEndpointTests
{
    private readonly CascadingFixture _fixture;
    private readonly ApoioDaPublicacaoDoProcessoSeletivo _apoio;

    public PublicarProcessoSeletivoRecusasEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaPublicacaoDoProcessoSeletivo(fixture);
    }

    [Fact(DisplayName =
        "POST /processos-seletivos/{id}/publicacao recusado por obrigatoriedade legal com checklist estrutural verde — 422 com obrigatoriedadesReprovadas e SEM pendencias (issue #1096 CA-05/CA-07)")]
    public async Task Publicar_QuandoSoObrigatoriedadeLegalReprovada_Retorna422SemPendencias()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Guid regraId = await SemearRegraQueReprovaTodoProcessoAsync(db);
        try
        {
            HttpResponseMessage response = await PublicarProcessoPublicavelAsync(client, db);
            await ConferirRecusaSoPorObrigatoriedadeLegalAsync(response);
        }
        finally
        {
            await DesativarRegraAsync(db, regraId);
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
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPendenciasSeeder.SemearComCadastroProprioNaoConformeAsync(
            db, nameof(Publicar_QuandoOutroErro422EChecklistAindaVermelho_IncluiPendencias), documentoConfirmado: false);

        HttpResponseMessage response = await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());

        await ConferirPendenciasMesmoEmRecusaAlheiaAsync(response);
    }

    private async Task<Guid> SemearRegraQueReprovaTodoProcessoAsync(UniPlusDbContext db)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        await TiposDeEtapaSeeder.SemearAsync(_fixture.Factory.Services, ObrigatoriedadeLegalNaoAtendidaSeeder.TipoEtapaNaoOfertada);
        return await ObrigatoriedadeLegalNaoAtendidaSeeder.SemearAsync(db, $"TEST_GATE_{Guid.CreateVersion7():N}");
    }

    private static async Task<HttpResponseMessage> PublicarProcessoPublicavelAsync(HttpClient client, UniPlusDbContext db)
    {
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, new OpcoesDoProcessoSeletivoPublicavel { Nome = nameof(Publicar_QuandoSoObrigatoriedadeLegalReprovada_Retorna422SemPendencias) });

        return await PostPublicarAsync(client, processo.Id, documento.Id, MakeIdempotencyKey());
    }

    private static async Task ConferirRecusaSoPorObrigatoriedadeLegalAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.conformidade_legal_insuficiente");
        doc.RootElement.GetProperty("obrigatoriedadesReprovadas").GetArrayLength().Should().BeGreaterThan(0);
        doc.RootElement.TryGetProperty("pendencias", out _).Should().BeFalse(
            "o checklist estrutural está 100% verde — só a obrigatoriedade legal reprovou");
    }

    /// <summary>
    /// A regra é universal (TipoProcessoUniversal) e sem vigenciaFim — sem esta limpeza, ela continuaria
    /// reprovando todo processo SiSU publicado depois dela neste mesmo container Postgres, compartilhado
    /// por toda a classe via CascadingFixture (afeta até os testes que esperam 204, como
    /// Publicar_FluxoCompleto_DispatchaCascadingMessages).
    /// </summary>
    private static Task<int> DesativarRegraAsync(UniPlusDbContext db, Guid regraId) =>
        db.Database.ExecuteSqlAsync($"UPDATE selecao.obrigatoriedades_legais SET is_deleted = true WHERE id = {regraId}");

    /// <summary>
    /// A recusa é por documento não confirmado — nada a ver com conformidade estrutural —, mas o
    /// checklist reconsultado no momento do enriquecimento ainda tem item vermelho, e o contrato
    /// (issue #1096) diz que "pendencias" é o estado atual, não uma tradução exclusiva do código de
    /// erro que efetivamente recusou.
    /// </summary>
    private static async Task ConferirPendenciasMesmoEmRecusaAlheiaAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString()
            .Should().Be("uniplus.selecao.processo_seletivo.documento_nao_confirmado");
        doc.RootElement.GetProperty("pendencias").EnumerateArray()
            .Select(e => e.GetProperty("codigo").GetString())
            .Should().Contain("atendimento_especializado_ausente");
    }
}
