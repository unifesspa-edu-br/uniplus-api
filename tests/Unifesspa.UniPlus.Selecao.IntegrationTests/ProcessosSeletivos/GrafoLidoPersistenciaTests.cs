namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

public sealed class GrafoLidoPersistenciaTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Fact]
    public async Task Persistir_GrafoInteiroLido_ConservaVinculosEBytes()
    {
        ProcessoSeletivo origem = CorpusEnvelopeProcessoRico.ProcessoRico(1);
        SnapshotCanonico congelado = CorpusEnvelope.Codec.Codificar(CorpusEnvelopeEntrada.Entrada(origem));
        VersaoConfiguracao versao = CorpusEnvelopeEntrada.VersaoDeAbertura(origem, congelado.Bytes);
        EnvelopeReidratado lido = ApoioDoEnvelopeCodec.Reidratado(versao);
        ProcessoSeletivo candidata = ProcessoDoGrafoLidoDeTeste.Criar(origem, lido.Grafo);
        await GravarAsync(candidata);
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo relido = (await ProcessoSeletivoRepositoryDeTeste.Criar(db)
            .ObterComConfiguracaoAsync(candidata.Id, CancellationToken.None))!;
        relido.Id.Should().NotBe(origem.Id);
        ConferirVinculos(relido, origem);
        ConferirEtapa(relido.Etapas.Single(e => e.Nome == "Prova Objetiva"), origem.Etapas.Single(e => e.Nome == "Prova Objetiva"));
        FidelidadeDoEnvelopeDeTeste.Recodificar(relido, versao, lido).Bytes.Should().Equal(congelado.Bytes);
    }

    private async Task GravarAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        db.Add(processo);
        await db.SaveChangesAsync();
    }

    private static void ConferirVinculos(ProcessoSeletivo relido, ProcessoSeletivo origem)
    {
        relido.Etapas.Select(e => e.Id).Should().BeEquivalentTo(origem.Etapas.Select(e => e.Id));
        relido.Etapas.Should().OnlyContain(e => e.ProcessoSeletivoId == relido.Id);
        relido.CronogramaFases.Select(f => f.Id).Should().BeEquivalentTo(origem.CronogramaFases.Select(f => f.Id));
        relido.DistribuicaoVagas.Should().HaveCount(origem.DistribuicaoVagas.Count);
        relido.DistribuicaoVagas.Should().OnlyContain(d => d.ProcessoSeletivoId == relido.Id);
        relido.DistribuicaoVagas.SelectMany(d => d.Modalidades).Should().OnlyContain(m =>
            relido.DistribuicaoVagas.Any(d => d.Id == m.ConfiguracaoDistribuicaoVagasId));
        ConferirOutrosVinculos(relido);
        ConferirQuantidades(relido, origem);
        relido.UnidadeAdministradoraOrigemId.Should().Be(origem.UnidadeAdministradoraOrigemId);
        relido.UnidadeAdministradora.Sigla.Should().Be(origem.UnidadeAdministradora.Sigla);
    }

    private static void ConferirOutrosVinculos(ProcessoSeletivo processo)
    {
        processo.CriteriosDesempate.Should().OnlyContain(c => c.ProcessoSeletivoId == processo.Id);
        processo.AtendimentoEspecializado!.ProcessoSeletivoId.Should().Be(processo.Id);
        processo.BonusRegional!.ProcessoSeletivoId.Should().Be(processo.Id);
        processo.Classificacao!.ProcessoSeletivoId.Should().Be(processo.Id);
        processo.Classificacao.RegrasEliminacao.Should().OnlyContain(r => r.ConfiguracaoClassificacaoId == processo.Classificacao.Id);
        processo.AtendimentoEspecializado.Condicoes.Should().OnlyContain(c => c.AtendimentoEspecializadoId == processo.AtendimentoEspecializado.Id);
    }

    private static void ConferirQuantidades(ProcessoSeletivo processo, ProcessoSeletivo origem)
    {
        processo.Etapas.Should().HaveCount(4);
        processo.DistribuicaoVagas.Should().HaveCount(2);
        processo.CriteriosDesempate.Should().HaveCount(origem.CriteriosDesempate.Count).And.NotBeEmpty();
        processo.Classificacao!.RegrasEliminacao.Should().HaveCount(origem.Classificacao!.RegrasEliminacao.Count).And.NotBeEmpty();
        processo.AtendimentoEspecializado!.Condicoes.Should().HaveCount(2);
        processo.AtendimentoEspecializado.TiposDeficiencia.Should().HaveCount(2);
        processo.BonusRegional!.Fator.Should().Be(1.2000m);
    }

    private static void ConferirFilhasPresentes(EtapaProcesso etapa)
    {
        etapa.ItensDeAvaliacao.Should().NotBeEmpty();
        etapa.Produtos.Should().NotBeEmpty();
        etapa.Bancas.Should().NotBeEmpty();
        etapa.Recursos.Should().NotBeEmpty();
    }

    private static void ConferirEtapa(EtapaProcesso relida, EtapaProcesso origem)
    {
        ConferirFilhasPresentes(relida);
        (relida.Inicio, relida.Fim, relida.EmiteParecerIndividual).Should().Be((origem.Inicio, origem.Fim, origem.EmiteParecerIndividual));
        relida.ItensDeAvaliacao.Select(i => (i.Id, i.Rotulo, i.PontuacaoMaxima))
            .Should().BeEquivalentTo(origem.ItensDeAvaliacao.Select(i => (i.Id, HashCanonicalComputer.NormalizeNfc(i.Rotulo), i.PontuacaoMaxima)));
        relida.Produtos.Select(p => (p.Id, p.AtoCodigo, p.Papel)).Should().BeEquivalentTo(
            origem.Produtos.Select(p => (p.Id, HashCanonicalComputer.NormalizeNfc(p.AtoCodigo), p.Papel)));
        relida.Bancas.Select(b => (b.TipoBancaOrigemId, b.Codigo)).Should().BeEquivalentTo(
            origem.Bancas.Select(b => (b.TipoBancaOrigemId, HashCanonicalComputer.NormalizeNfc(b.Codigo))));
        relida.Recursos.Select(r => (r.Ancora, r.Regra.Codigo, r.Parametros.PrazoValor, r.ProdutoAncoraId)).Should().BeEquivalentTo(
            origem.Recursos.Select(r => (r.Ancora, r.Regra.Codigo, r.Parametros.PrazoValor, r.ProdutoAncoraId)));
    }
}
