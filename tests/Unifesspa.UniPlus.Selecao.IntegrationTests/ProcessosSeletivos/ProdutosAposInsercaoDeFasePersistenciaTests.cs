namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ProdutosAposInsercaoDeFasePersistenciaTests(ProcessoSeletivoDbFixture fixture) : IClassFixture<ProcessoSeletivoDbFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinirCronograma_InserirFaseNoMeio_ConservaProdutoEReferenciaDoRecurso(bool comRecurso)
    {
        (ProcessoSeletivo processo, FaseCronograma inscricao) = ApoioDoEnvelopeCodec.ProcessoDeInscricao("PS identidade do produto");
        Guid origem = Guid.CreateVersion7();
        processo.DefinirCronogramaFases([inscricao, Fase(2, origem, comRecurso)], []).IsSuccess.Should().BeTrue();
        Guid produto = processo.CronogramaFases.Single(f => f.Ordem == 2).Produtos.Single(p => p.Papel == PapelDaPublicacaoPrevista.Preliminar).Id;
        await PersistirAsync(processo);
        await InserirFaseAsync(processo.Id, origem, comRecurso, produto);
        await ConferirAsync(processo.Id, produto, comRecurso);
    }

    private async Task PersistirAsync(ProcessoSeletivo processo)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        db.Add(processo);
        await db.SaveChangesAsync();
    }

    private async Task InserirFaseAsync(Guid id, Guid origem, bool comRecurso, Guid produtoAnterior)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(id, CancellationToken.None))!;
        FaseCronograma nova = Fase(3, origem, comRecurso);
        nova.Produtos.Single(p => p.Papel == PapelDaPublicacaoPrevista.Preliminar).Id.Should().NotBe(produtoAnterior);
        FaseCronograma inserida = FaseCronograma.Criar(ChaveLocal.Criar("fase_habilitacao").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NaExecucao, false, false, false, false)),
            new DecisoesDeCronogramaDaFase(2, null, null, UnidadesDeTeste.Ceps), new PublicacoesDaFase([], false, null), []).Value!;
        processo.DefinirCronogramaFases([processo.CronogramaFases.Single(f => f.Ordem == 1), inserida, nova], []).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private async Task ConferirAsync(Guid id, Guid produtoId, bool comRecurso)
    {
        await using UniPlusDbContext db = fixture.CreateDbContext();
        ProcessoSeletivo processo = (await ProcessoSeletivoRepositoryDeTeste.Criar(db).ObterComConfiguracaoAsync(id, CancellationToken.None))!;
        processo.CronogramaFases.Should().HaveCount(3);
        FaseCronograma fase = processo.CronogramaFases.Single(f => f.Ordem == 3);
        ProdutoDaFase produto = fase.Produtos.Single(p => p.Papel == PapelDaPublicacaoPrevista.Preliminar);
        produto.Id.Should().Be(produtoId);
        produto.FaseCronogramaId.Should().Be(fase.Id);
        processo.CronogramaFases.SelectMany(f => f.Produtos).Select(p => p.Id).Should().OnlyHaveUniqueItems();
        if (comRecurso)
        {
            fase.RegraRecurso!.ProdutoAncoraId.Should().Be(produtoId);
        }
    }

    private static FaseCronograma Fase(int ordem, Guid origem, bool comRecurso)
    {
        ProdutoDaFase preliminar = ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar);
        return FaseCronograma.Criar(ChaveLocal.Criar("fase_resultado_final").Value!,
            OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(origem, "RESULTADO_FINAL", OrigemDataFase.NaExecucao, false, false, false, false)),
            new DecisoesDeCronogramaDaFase(ordem, null, null, UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([preliminar, ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo)], false,
                comRecurso ? Recurso(preliminar.Id) : null), comRecurso ? [BancasDeTeste.DeRecursosDaFase()] : []).Value!;
    }

    private static RegraRecursoFase Recurso(Guid produto) => RegraRecursoFase.Criar(
        ExemploDeEnvelope.Regra(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), 'e'),
        ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas), produto,
        ProcedimentosDeRecurso.De([])).Value!;
}
