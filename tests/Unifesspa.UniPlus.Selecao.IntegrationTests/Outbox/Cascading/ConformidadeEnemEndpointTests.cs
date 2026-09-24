namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A conformidade do processo baseado em ENEM com cálculo local pelo HTTP: publicar e retificar
/// recusam a oferta sem grupo de área com o mesmo item do checklist estrutural.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class ConformidadeEnemEndpointTests
{
    private const string ItemOfertaSemGrupo = "distribuicao_vagas_oferta_sem_grupo_area_enem";

    private readonly CascadingFixture _fixture;

    public ConformidadeEnemEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "POST /publicacao de processo ENEM com cálculo local e oferta sem grupo — 422 ConformidadeInsuficiente com o item da oferta nas pendências")]
    public async Task Publicar_OfertaSemGrupo_Retorna422ComOItem()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await SemearAsync(api, nameof(Publicar_OfertaSemGrupo_Retorna422ComOItem), ofertaComGrupo: false);
        Guid oferta = processo.DistribuicaoVagas.Single().OfertaCursoOrigemId;

        HttpResponseMessage response = await PostAsync(client, $"{processo.Id}/publicacao", NovoCorpoPublicacao(documento.Id));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.processo_seletivo.conformidade_insuficiente");
        JsonElement pendencia = doc.RootElement.GetProperty("pendencias").EnumerateArray()
            .Single(e => e.GetProperty("codigo").GetString() == ItemOfertaSemGrupo);
        pendencia.GetProperty("mensagem").GetString().Should().Contain(oferta.ToString());
    }

    [Fact(DisplayName = "POST /retificacoes de processo ENEM com cálculo local cuja oferta ficou sem grupo — 422 ConformidadeInsuficiente com a oferta no detalhe")]
    public async Task Retificar_OfertaSemGrupo_Retorna422ComOItem()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();
        (ProcessoSeletivo processo, DocumentoEdital documento) = await SemearAsync(api, nameof(Retificar_OfertaSemGrupo_Retorna422ComOItem), ofertaComGrupo: true);
        Guid oferta = processo.DistribuicaoVagas.Single().OfertaCursoOrigemId;

        (await PostAsync(client, $"{processo.Id}/publicacao", NovoCorpoPublicacao(documento.Id)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid documentoRetificacao;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            // Correção de dados sobre o processo publicado: o grupo perde o código, e o EF
            // devolve a oferta sem grupo.
            await db.Database.ExecuteSqlAsync(
                $"UPDATE selecao.configuracoes_distribuicao_vagas SET grupo_area_enem_codigo = NULL WHERE processo_seletivo_id = {processo.Id}");
            DocumentoEdital retificacao = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
            retificacao.Confirmar(2048, string.Concat(Enumerable.Repeat("cd45670189", 7))[..64], TimeProvider.System)
                .IsSuccess.Should().BeTrue();
            await db.DocumentosEdital.AddAsync(retificacao);
            await db.SaveChangesAsync();
            documentoRetificacao = retificacao.Id;
        }

        HttpResponseMessage response = await PostAsync(client, $"{processo.Id}/retificacoes", NovoCorpoRetificacao(documentoRetificacao));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.processo_seletivo.conformidade_insuficiente");
        // A resposta de retificar não traz as pendências: a oferta pendente vem no detalhe.
        doc.RootElement.GetProperty("detail").GetString().Should().Contain(oferta.ToString());
    }

    private static async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearAsync(
        CascadingApiFactory api, string nome, bool ofertaComGrupo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await ProcessoSeletivoPendenciasSeeder.SemearEnemComCalculoLocalAsync(db, $"{nome} {Guid.CreateVersion7()}", ofertaComGrupo);
    }

    private static object Ato(string tipoAtoCodigo) => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo,
    };

    private static object NovoCorpoPublicacao(Guid documentoEditalId) => new
    {
        numero = "001/2026",
        documentoEditalId,
        ato = Ato("EDITAL_ABERTURA"),
    };

    private static object NovoCorpoRetificacao(Guid documentoEditalId) => new
    {
        motivo = "Correção do grupo de área da oferta",
        numero = "001/2026-R1",
        documentoEditalId,
        ato = Ato("EDITAL_RETIFICACAO"),
    };

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string rota, object corpo)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{rota}", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request).ConfigureAwait(false);
    }
}
