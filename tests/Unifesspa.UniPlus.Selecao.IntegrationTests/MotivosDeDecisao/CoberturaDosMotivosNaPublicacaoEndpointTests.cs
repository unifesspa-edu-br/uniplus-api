namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O portão de cobertura dos motivos na publicação pelo endpoint real, contra Postgres via Testcontainers
/// (UNI-REQ-0123): com um fundamento de isenção habilitado, a publicação exige ao menos um motivo
/// favorável e um desfavorável escolhidos para esse fundamento.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class CoberturaDosMotivosNaPublicacaoEndpointTests : IAsyncLifetime
{
    private const string Fundamento = "CADASTRO_UNICO";
    private readonly CascadingFixture _fixture;

    public CoberturaDosMotivosNaPublicacaoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_do_processo WHERE codigo LIKE 'TEST\\_CDM\\_%' ESCAPE '\\'");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_CDM\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "Fundamento de isenção só com motivo desfavorável recusa a publicação citando o fundamento e o sentido favorável")]
    public async Task Publicar_SemMotivoFavoravel_RecusaComCoberturaInsuficiente()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComIsencaoAsync(nameof(Publicar_SemMotivoFavoravel_RecusaComCoberturaInsuficiente));
        using HttpClient client = _fixture.Factory.CreateClient();
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Desfavoravel);

        using HttpResponseMessage recusada = await PublicarAsync(client, processoId, documentoId);

        string corpo = Regex.Unescape(await recusada.Content.ReadAsStringAsync());
        recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        corpo.Should().Contain("uniplus.selecao.motivo_de_decisao.cobertura_insuficiente")
            .And.Contain($"motivo favorável para o fundamento de isenção {Fundamento}")
            .And.NotContain("motivo desfavorável para o fundamento", "o sentido desfavorável já está coberto");
    }

    [Fact(DisplayName = "Com motivo favorável e desfavorável no fundamento habilitado, a publicação passa")]
    public async Task Publicar_ComOsDoisSentidos_Publica()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoComIsencaoAsync(nameof(Publicar_ComOsDoisSentidos_Publica));
        using HttpClient client = _fixture.Factory.CreateClient();
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Desfavoravel);
        await IncluirNovoMotivoAsync(client, processoId, SentidoDoMotivo.Favoravel);

        using HttpResponseMessage publicada = await PublicarAsync(client, processoId, documentoId);

        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoComIsencaoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"{nome} {Guid.CreateVersion7()}", complementar: HabilitarIsencao);
        return (processo.Id, documento.Id);
    }

    /// <summary>Abre a janela da isenção em fase própria, que fecha antes das inscrições, e cobra a taxa com um fundamento.</summary>
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
        ConfiguracaoTaxaInscricao taxa = ConfiguracaoTaxaInscricao.Criar(cobra: true, valor: 100m, fundamentosCodigos: [Fundamento]).Value!;
        processo.DefinirTaxaInscricao(taxa, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private async Task IncluirNovoMotivoAsync(HttpClient client, Guid processoId, SentidoDoMotivo sentido)
    {
        MotivoDeDecisao motivo = MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.IsencaoDaTaxa,
            $"TEST_CDM_{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            "Situação no Cadastro Único conferida pela comissão",
            Fundamento,
            sentido)).Value!;
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            db.MotivosDeDecisao.Add(motivo);
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage inclusao = await EnviarAsync(
            client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/motivos-de-decisao/{motivo.Id}", new { justificativa = (string?)null });
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoId) =>
        EnviarAsync(client, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", new
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
        });

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object corpo)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative)) { Content = JsonContent.Create(corpo) };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
