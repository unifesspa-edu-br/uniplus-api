namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A inclusão de motivo no processo publicado pelo endpoint real (UNI-REQ-0124, UNI-REQ-0285): só
/// sob a sessão de retificação, com justificativa e antes do início da avaliação daquela decisão. A
/// justificativa é de uso interno: a leitura dos motivos do processo exige configurar o processo.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class InclusaoNaRetificacaoEndpointTests : IAsyncLifetime
{
    private const string Fundamento = "CADASTRO_UNICO";
    private const string Rota = "/api/selecao/processos-seletivos";
    private readonly CascadingFixture _fixture;

    public InclusaoNaRetificacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_do_processo WHERE codigo LIKE 'TEST\\_IRE\\_%' ESCAPE '\\'");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_IRE\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "Na retificação, a inclusão com justificativa é aceita; sem ela, ou depois de iniciada a avaliação, é recusada")]
    public async Task Retificacao_InclusaoAceitaERecusas()
    {
        Guid processoId = await SemearPublicadoComIsencaoAsync(nameof(Retificacao_InclusaoAceitaERecusas));
        string etag = await AbrirRetificacaoAsync(processoId);
        MotivoDeDecisao homologacao = await SemearMotivoAsync(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, SentidoDoMotivo.Desfavoravel);
        MotivoDeDecisao outraHomologacao = await SemearMotivoAsync(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, SentidoDoMotivo.Favoravel);
        MotivoDeDecisao isencao = await SemearMotivoAsync(TipoMotivoDeDecisao.IsencaoDaTaxa, Fundamento, SentidoDoMotivo.Desfavoravel);

        using HttpResponseMessage semJustificativa = await IncluirAsync(processoId, outraHomologacao.Id, null, etag);
        using HttpResponseMessage aceita = await IncluirAsync(processoId, homologacao.Id, "Retorno do INEP passou a ser exigido pela norma.", etag);
        string etagNovo = aceita.Headers.ETag!.Tag;
        using HttpResponseMessage avaliacaoIniciada = await IncluirAsync(processoId, isencao.Id, "Motivo previsto em norma nova.", etagNovo);

        (await CodigoDoErro(semJustificativa)).Should().Be("uniplus.selecao.motivo_de_decisao.justificativa_obrigatoria");
        aceita.StatusCode.Should().Be(HttpStatusCode.NoContent, await aceita.Content.ReadAsStringAsync());
        (await CodigoDoErro(avaliacaoIniciada)).Should().Be("uniplus.selecao.motivo_de_decisao.avaliacao_iniciada",
            "a janela do pedido de isenção já começou, e a avaliação dessa decisão com ela");
    }

    [Fact(DisplayName = "Descartar a sessão de retificação retira o motivo incluído nela e mantém os congelados na publicação")]
    public async Task Descarte_RetiraPendenteEMantemCongelados()
    {
        Guid processoId = await SemearPublicadoComIsencaoAsync(nameof(Descarte_RetiraPendenteEMantemCongelados));
        string etag = await AbrirRetificacaoAsync(processoId);
        MotivoDeDecisao novo = await SemearMotivoAsync(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, SentidoDoMotivo.Desfavoravel);
        using HttpResponseMessage inclusao = await IncluirAsync(processoId, novo.Id, "Retorno do INEP passou a ser exigido pela norma.", etag);
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());

        using HttpResponseMessage descarte = await EnviarAsync(HttpMethod.Delete, $"{Rota}/{processoId}/retificacao-em-curso", new { }, inclusao.Headers.ETag!.Tag);

        descarte.IsSuccessStatusCode.Should().BeTrue(await descarte.Content.ReadAsStringAsync());
        IReadOnlyList<JsonElement> escolhidos = await EscolhidosAsync(processoId);
        escolhidos.Select(static e => e.GetProperty("codigo").GetString()).Should().NotContain(novo.Codigo.Valor);
        escolhidos.Should().HaveCount(2).And.OnlyContain(static e => e.GetProperty("congelado").GetBoolean());
    }

    [Fact(DisplayName = "A justificativa da inclusão não sai em leitura anônima nem de quem não configura o processo")]
    public async Task Justificativa_NaoSaiEmLeituraPublica()
    {
        Guid processoId = await SemearPublicadoComIsencaoAsync(nameof(Justificativa_NaoSaiEmLeituraPublica));
        string etag = await AbrirRetificacaoAsync(processoId);
        MotivoDeDecisao homologacao = await SemearMotivoAsync(TipoMotivoDeDecisao.HomologacaoDasInscricoes, null, SentidoDoMotivo.Desfavoravel);
        const string justificativa = "Justificativa interna da administração";
        (await IncluirAsync(processoId, homologacao.Id, justificativa, etag)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage anonimo = await LerAsync($"{Rota}/{processoId}/motivos-de-decisao", sub: null, papel: null);
        using HttpResponseMessage candidata = await LerAsync($"{Rota}/{processoId}/motivos-de-decisao", sub: "marlene.martins", papel: null);
        using HttpResponseMessage processoPublico = await LerAsync($"{Rota}/{processoId}", sub: null, papel: null);
        using HttpResponseMessage quemConfigura = await LerAsync($"{Rota}/{processoId}/motivos-de-decisao", sub: null, papel: "plataforma-admin");

        anonimo.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        candidata.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await processoPublico.Content.ReadAsStringAsync()).Should().NotContain(justificativa);
        (await quemConfigura.Content.ReadAsStringAsync()).Should().Contain(justificativa);
    }

    private async Task<Guid> SemearPublicadoComIsencaoAsync(string nome)
    {
        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
                db, $"{nome} {Guid.CreateVersion7()}", complementar: HabilitarIsencao);
            (processoId, documentoId) = (processo.Id, documento.Id);
        }

        foreach (SentidoDoMotivo sentido in new[] { SentidoDoMotivo.Favoravel, SentidoDoMotivo.Desfavoravel })
        {
            MotivoDeDecisao motivo = await SemearMotivoAsync(TipoMotivoDeDecisao.IsencaoDaTaxa, Fundamento, sentido);
            (await IncluirAsync(processoId, motivo.Id, null, null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        using HttpResponseMessage publicada = await EnviarAsync(HttpMethod.Post, $"{Rota}/{processoId}/publicacao", CorpoDaPublicacao(documentoId), null);
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
        return processoId;
    }

    /// <summary>Abre o pedido de isenção em fase própria, já iniciada, e cobra a taxa com um fundamento.</summary>
    private static void HabilitarIsencao(ProcessoSeletivo processo)
    {
        FaseCronograma isencao = FaseCronograma.Criar(
            ordem: 2, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "ISENCAO_DA_TAXA",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: false, permiteComplementacao: false,
            coletaInscricao: false, coletaSolicitacaoIsencao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            produtos: [], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null,
            unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;
        processo.DefinirCronogramaFases([processo.CronogramaFases.Single(), isencao], [], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTaxaInscricao(ConfiguracaoTaxaInscricao.Criar(cobra: true, valor: 100m, fundamentosCodigos: [Fundamento]).Value!, PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    private async Task<string> AbrirRetificacaoAsync(Guid processoId)
    {
        using HttpResponseMessage abertura = await EnviarAsync(
            HttpMethod.Post, $"{Rota}/{processoId}/retificacao-em-curso", new { motivo = "Inclusão de motivo de decisão" }, null);
        abertura.StatusCode.Should().Be(HttpStatusCode.Created, await abertura.Content.ReadAsStringAsync());
        return abertura.Headers.ETag!.Tag;
    }

    private async Task<MotivoDeDecisao> SemearMotivoAsync(TipoMotivoDeDecisao tipo, string? subclassificacao, SentidoDoMotivo sentido)
    {
        MotivoDeDecisao motivo = MotivoDeDecisao.Criar(new CamposDoMotivo(
            tipo, $"TEST_IRE_{Guid.NewGuid():N}"[..40].ToUpperInvariant(), "Motivo previsto pela comissão do processo", subclassificacao, sentido)).Value!;
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        db.MotivosDeDecisao.Add(motivo);
        await db.SaveChangesAsync();
        return motivo;
    }

    private Task<HttpResponseMessage> IncluirAsync(Guid processoId, Guid motivoId, string? justificativa, string? etag) =>
        EnviarAsync(HttpMethod.Put, $"{Rota}/{processoId}/motivos-de-decisao/{motivoId}", new { justificativa }, etag);

    private static object CorpoDaPublicacao(Guid documentoId) => new
    {
        numero = (string?)null,
        documentoEditalId = documentoId,
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

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, object corpo, string? etag)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        HttpResponseMessage resposta = await client.SendAsync(request);
        await resposta.Content.LoadIntoBufferAsync();
        return resposta;
    }

    private async Task<HttpResponseMessage> LerAsync(string url, string? sub, string? papel)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(url, UriKind.Relative));
        if (sub is not null || papel is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        }

        if (sub is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        }

        if (papel is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        HttpResponseMessage resposta = await client.SendAsync(request);
        await resposta.Content.LoadIntoBufferAsync();
        return resposta;
    }

    private async Task<IReadOnlyList<JsonElement>> EscolhidosAsync(Guid processoId)
    {
        using HttpResponseMessage leitura = await LerAsync($"{Rota}/{processoId}/motivos-de-decisao", sub: null, papel: "plataforma-admin");
        leitura.StatusCode.Should().Be(HttpStatusCode.OK, await leitura.Content.ReadAsStringAsync());
        return [.. (await leitura.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private static async Task<string?> CodigoDoErro(HttpResponseMessage resposta)
    {
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }
}
