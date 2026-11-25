namespace Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem;

using System.Net;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ApoioDaImportacaoDeNotasDeTeste;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.NotasDoEnem.ProcessoDoEnemPublicadoDeTeste;
using static Unifesspa.UniPlus.Testes.Compartilhado.ArquivoDoInepSintetico;

/// <summary>
/// As avaliações que a importação grava por oferta concorrida: a nota exata de cada grupo, o conflito de CPF, o
/// lote novo que deixa sem nota quem não está nele e a leitura das avaliações vigentes.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class AvaliacoesGravadasPelaImportacaoDeNotasTests
{
    private readonly CascadingFixture _fixture;
    private readonly ApoioDaImportacaoDeNotasDeTeste _apoio;

    public AvaliacoesGravadasPelaImportacaoDeNotasTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaImportacaoDeNotasDeTeste(fixture);
    }

    private async Task<IReadOnlyList<AvaliacaoDaEtapa>> AvaliacoesDoLoteAsync(Guid loteId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.AvaliacoesDaEtapa.AsNoTracking().Where(a => a.LoteId == loteId).ToListAsync();
    }

    // Notas do arquivo sintético por área: CN 611.1, CH 722.2, LC 833.3, MT 544.4, Redação 960. Os
    // pesos são os do quadro de teste: Redação 2, CN 1,5, CH 2,5, MT 1,5 e Linguagens 1 (Tecnológica) ou 3 (Humanística I).
    private const decimal SomaPonderadaSemLinguagens = (960m * 2m) + (611.1m * 1.5m) + (722.2m * 2.5m) + (544.4m * 1.5m);
    private static readonly decimal NotaDeFernandaNaTecnologica = (SomaPonderadaSemLinguagens + (833.3m * 1m)) / 8.5m;
    private static readonly decimal NotaDeFernandaNaHumanistica = (SomaPonderadaSemLinguagens + (833.3m * 3m)) / 10.5m;

    [Fact(DisplayName = "a importação grava uma avaliação por oferta concorrida, com a nota exata de cada grupo, e sem nota para quem não tem linha")]
    public async Task Importar_GravaAvaliacaoPorOfertaConcorrida()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0, 1);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage resposta = await ImportarAsync(client, cenario, Bytes([Linha(Pessoas[0], 1)]));

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        IReadOnlyList<AvaliacaoDaEtapa> avaliacoes = await AvaliacoesDoLoteAsync(await IdDoLoteAsync(resposta));
        ConferirNotasPorOferta(avaliacoes, cenario);
    }

    private static void ConferirNotasPorOferta(IReadOnlyList<AvaliacaoDaEtapa> avaliacoes, Cenario cenario)
    {
        avaliacoes.Should().HaveCount(4);
        AvaliacaoDaEtapa naTecnologica = avaliacoes.Single(a => a.InscricaoId == cenario.Inscricoes[0] && a.OfertaCursoId == cenario.Processo.OfertaTecnologicaId);
        AvaliacaoDaEtapa naHumanistica = avaliacoes.Single(a => a.InscricaoId == cenario.Inscricoes[0] && a.OfertaCursoId == cenario.Processo.OfertaHumanisticaId);
        naTecnologica.Situacao.Should().Be(SituacaoDaAvaliacao.Apurada);
        naTecnologica.GrupoAreaEnemCodigo.Should().Be("TECNOLOGICA");
        naTecnologica.Nota.Should().Be(NotaDeFernandaNaTecnologica);
        naHumanistica.GrupoAreaEnemCodigo.Should().Be("HUMANISTICA_I");
        naHumanistica.Nota.Should().Be(NotaDeFernandaNaHumanistica);
        avaliacoes.Where(a => a.InscricaoId == cenario.Inscricoes[1]).Should().HaveCount(2)
            .And.OnlyContain(a => a.Situacao == SituacaoDaAvaliacao.InscritoSemLinha && a.Nota == null);
    }

    [Fact(DisplayName = "a leitura das avaliações vigentes traz só o lote vigente e esconde a inscrição que trocou de edição ou de oferta depois da importação")]
    public async Task Leitor_AvaliacoesVigentes_EscondeOQueNaoBate()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0, 1, 2);
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await ImportarAsync(client, cenario, Bytes([Linha(Pessoas[0], 1), LinhaDeAusente(Pessoas[1], 2), Linha(Pessoas[2], 3)]));
        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        Guid lote = await IdDoLoteAsync(resposta);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await AlterarEdicaoEOfertaAsync(db, cenario);
        IAvaliacoesDaEtapaReader leitor = scope.ServiceProvider.GetRequiredService<IAvaliacoesDaEtapaReader>();

        IReadOnlyList<AvaliacaoVigente> vigentes = await leitor.ListarVigentesAsync(
            cenario.Processo.EtapaDoEnemId, [.. cenario.Inscricoes.Values]);

        ConferirAvaliacoesVigentes(vigentes, cenario, lote);
    }

    private static void ConferirAvaliacoesVigentes(IReadOnlyList<AvaliacaoVigente> vigentes, Cenario cenario, Guid lote)
    {
        vigentes.Should().OnlyContain(a => a.LoteId == lote && a.InscricaoId != cenario.Inscricoes[2]);
        vigentes.Where(a => a.InscricaoId == cenario.Inscricoes[0]).Should().HaveCount(2).And.OnlyContain(a => a.Nota != null);
        vigentes.Where(a => a.InscricaoId == cenario.Inscricoes[1]).Should().ContainSingle()
            .Which.OfertaCursoId.Should().Be(cenario.Processo.OfertaTecnologicaId);
    }

    private static async Task AlterarEdicaoEOfertaAsync(UniPlusDbContext db, Cenario cenario)
    {
        await db.RespostasDaInscricao
            .Where(r => r.InscricaoId == cenario.Inscricoes[2] && r.FatoCodigo == Unifesspa.UniPlus.Regras.Services.EdicaoDoEnem.Fato)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ValorCodigo, "2024"));
        await db.Inscricoes.Where(i => i.Id == cenario.Inscricoes[1])
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.OfertaCursoSegundaOpcaoId, (Guid?)null));
    }

    [Fact(DisplayName = "duas linhas diferentes do mesmo CPF deixam a inscrição sem nota, em conflito")]
    public async Task Importar_Conflito_SemNota()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0);
        using HttpClient client = _fixture.Factory.CreateClient();
        byte[] arquivo = Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[0], 1, ["100", "722.2", "833.3", "544.4", "960"])]);

        HttpResponseMessage resposta = await ImportarAsync(client, cenario, arquivo);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        IReadOnlyList<AvaliacaoDaEtapa> avaliacoes = await AvaliacoesDoLoteAsync(await IdDoLoteAsync(resposta));
        avaliacoes.Should().HaveCount(2).And.OnlyContain(a => a.Situacao == SituacaoDaAvaliacao.EmConflito && a.Nota == null);
    }

    [Fact(DisplayName = "o lote novo traz as avaliações dele: quem não está no arquivo novo fica sem nota nele, e o lote anterior segue intacto")]
    public async Task Importar_LoteNovoSemAInscricao_FicaSemNotaNele()
    {
        Cenario cenario = await _apoio.NovoCenarioAsync(0, 1);
        using HttpClient client = _fixture.Factory.CreateClient();
        byte[] comFernanda = Bytes([Linha(Pessoas[0], 1), Linha(Pessoas[1], 2)]);
        byte[] semFernanda = Bytes([Linha(Pessoas[1], 2)]);

        Guid loteA = await IdDoLoteAsync(await ImportarAsync(client, cenario, comFernanda));
        Guid loteB = await IdDoLoteAsync(await ImportarAsync(client, cenario, semFernanda));
        Guid loteDeNovoA = await IdDoLoteAsync(await ImportarAsync(client, cenario, comFernanda));

        loteDeNovoA.Should().NotBe(loteA, "o arquivo de um lote substituído cria lote novo");
        await ConferirAvaliacoesDosLotesAsync(cenario, loteA, loteB, loteDeNovoA);
    }

    private async Task ConferirAvaliacoesDosLotesAsync(Cenario cenario, Guid loteA, Guid loteB, Guid loteDeNovoA)
    {
        (await AvaliacoesDoLoteAsync(loteB)).Where(a => a.InscricaoId == cenario.Inscricoes[0])
            .Should().HaveCount(2).And.OnlyContain(a => a.Situacao == SituacaoDaAvaliacao.InscritoSemLinha && a.Nota == null);
        (await AvaliacoesDoLoteAsync(loteA)).Where(a => a.InscricaoId == cenario.Inscricoes[0])
            .Should().HaveCount(2).And.OnlyContain(a => a.Situacao == SituacaoDaAvaliacao.Apurada && a.Nota != null);
        (await AvaliacoesDoLoteAsync(loteDeNovoA)).Where(a => a.InscricaoId == cenario.Inscricoes[0])
            .Should().OnlyContain(a => a.Situacao == SituacaoDaAvaliacao.Apurada);
    }
}
