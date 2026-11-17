namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

/// <summary>
/// O mínimo de integrantes da banca de recursos de uma etapa vem dos corretores que a regra do catálogo põe em campo
/// (os distintos e, quando a regra declara o novo corretor, mais um), lidos na publicação sobre a regra semeada no banco.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class MinimoDeIntegrantesNaPublicacaoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public MinimoDeIntegrantesNaPublicacaoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A publicação recusa a banca da etapa de correção independente com dois integrantes, abaixo dos três corretores da regra")]
    public async Task Publicar_BancaComDoisIntegrantes_Retorna422()
    {
        HttpResponseMessage resposta = await PublicarComBancaDeAsync(integrantes: 2, nameof(Publicar_BancaComDoisIntegrantes_Retorna422), hashDaReferencia: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.banca.integrantes_abaixo_do_minimo");
    }

    [Fact(DisplayName = "A publicação aceita a banca da etapa de correção independente com três integrantes")]
    public async Task Publicar_BancaComTresIntegrantes_Retorna204()
    {
        HttpResponseMessage resposta = await PublicarComBancaDeAsync(integrantes: 3, nameof(Publicar_BancaComTresIntegrantes_Retorna204), hashDaReferencia: null);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(DisplayName = "A publicação recusa com 422 a etapa cuja referência à regra de consolidação tem o hash diferente do catálogo, e o corpo traz as pendências")]
    public async Task Publicar_HashDaRegraDivergente_Retorna422ComPendencias()
    {
        HttpResponseMessage resposta = await PublicarComBancaDeAsync(
            integrantes: 3, nameof(Publicar_HashDaRegraDivergente_Retorna422ComPendencias), hashDaReferencia: new string('0', 64));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.etapa_processo.regra_de_consolidacao_nao_resolvida");
        problema.RootElement.GetProperty("pendencias").EnumerateArray().Select(static item => item.GetProperty("codigo").GetString())
            .Should().Contain("cronograma_banca_com_o_minimo_de_integrantes");
    }

    [Theory(DisplayName = "A publicação recusa a banca da etapa de maioria simples com novo corretor com três integrantes, abaixo dos três corretores e do adicional")]
    [InlineData(3, HttpStatusCode.UnprocessableEntity)]
    [InlineData(4, HttpStatusCode.NoContent)]
    public async Task Publicar_MaioriaSimplesComNovoCorretor_ExigeOsCorretoresMaisUm(int integrantes, HttpStatusCode esperado)
    {
        HttpResponseMessage resposta = await PublicarComBancaDeAsync(
            integrantes, $"{nameof(Publicar_MaioriaSimplesComNovoCorretor_ExigeOsCorretoresMaisUm)} {integrantes}", hashDaReferencia: null,
            RegraConsolidacaoCodigo.ConceitoMaioriaSimples);

        resposta.StatusCode.Should().Be(esperado);
        if (esperado == HttpStatusCode.UnprocessableEntity)
        {
            using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.banca.integrantes_abaixo_do_minimo");
            problema.RootElement.GetProperty("detail").GetString().Should().Contain("o mínimo exigido é 4");
        }
    }

    [Fact(DisplayName = "A publicação aceita a banca da etapa de unanimidade com decisão da banca com dois integrantes, sem corretor adicional")]
    public async Task Publicar_UnanimidadeComDecisaoDaBanca_Retorna204()
    {
        HttpResponseMessage resposta = await PublicarComBancaDeAsync(
            integrantes: 2, nameof(Publicar_UnanimidadeComDecisaoDaBanca_Retorna204), hashDaReferencia: null, RegraConsolidacaoCodigo.ConceitoUnanimidade);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<HttpResponseMessage> PublicarComBancaDeAsync(
        int integrantes, string cenario, string? hashDaReferencia, string codigoDaRegra = RegraConsolidacaoCodigo.CorrecaoIndependente)
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        using HttpClient client = api.CreateClient();

        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ReferenciaRegra doRol = (await db.RolDeRegras.AsNoTracking().SingleAsync(regra =>
            regra.Codigo == codigoDaRegra && regra.Versao == RegraCatalogoSeed.VersaoV1)).Referencia();
        ReferenciaRegra consolidacao = hashDaReferencia is null ? doRol : ReferenciaRegra.De(doRol.Codigo, doRol.Versao, hashDaReferencia);

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db, $"{cenario} {Guid.CreateVersion7()}", complementar: p => ComBancaDeRecursosNaEtapa(p, consolidacao, integrantes));

        return await PublicacaoPeloEndpointDeTeste.PublicarAsync(client, processo.Id, documento.Id);
    }

    private static void ComBancaDeRecursosNaEtapa(ProcessoSeletivo processo, ReferenciaRegra consolidacao, int integrantes)
    {
        EtapaProcesso etapa = EtapaProcesso.Criar(
            new IdentificacaoDaEtapa(
                Nome: "Redação",
                Carater: CaraterEtapa.Classificatoria,
                TipoEtapa: TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "REDACAO", "Redação", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
                Ordem: 1,
                FaseCodigo: "RESULTADO_FINAL"),
            new DeclaracaoDaAvaliacaoDaEtapa(
                Peso: 1m, NotaMinima: null, NotaMaxima: 10m, PassoDoLancamento: 0.5m,
                FormaDeAvaliacao: FormaDeAvaliacao.Humana, RegraConsolidacao: consolidacao, EscalaDeAtendimento: null)).Value!;
        etapa.DefinirBancas([BancasDeTeste.DeRecursosDaEtapa(BancasDeTeste.Regra(integrantes, quorum: 2))]).IsSuccess.Should().BeTrue();

        processo.DefinirEtapas([etapa]).IsSuccess.Should().BeTrue();
    }
}
