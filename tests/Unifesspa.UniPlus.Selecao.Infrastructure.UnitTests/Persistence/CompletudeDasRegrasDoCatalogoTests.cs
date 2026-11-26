namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

public sealed class CompletudeDasRegrasDoCatalogoTests
{
    [Fact]
    public void TipoRegra_SementeV1_ContemTodosOsTipos() =>
        RegraCatalogoSeed.Itens.Where(item => item.Versao == RegraCatalogoSeed.VersaoV1)
            .Select(static item => item.Tipo).Distinct()
            .Should().BeEquivalentTo(CatalogoDe<TipoRegra>.Valores());

    [Fact]
    public void RegraDoFada_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDoFada>(TipoRegra.RegraFada);

    [Fact]
    public void RegraDeArredondamentoDaNota_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeArredondamentoDaNota>(TipoRegra.RegraArredondamento);

    [Fact]
    public void RegraDeCalculoDaNota_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeCalculoDaNota>(TipoRegra.RegraCalculo);

    [Fact]
    public void RegraDeOrdemDeAlocacao_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeOrdemDeAlocacao>(TipoRegra.RegraOrdemAlocacao);

    [Fact]
    public void RegraDeAjusteDaDistribuicaoDeVagas_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeAjusteDaDistribuicaoDeVagas>(TipoRegra.RegraAjusteDistribuicaoVagas);

    [Fact]
    public void RegraDeDesempate_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeDesempate>(TipoRegra.CriterioDesempate);

    [Fact]
    public void RegraDeEliminacaoPorCalculo_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeEliminacaoPorCalculo>(TipoRegra.RegraEliminacao);

    [Fact]
    public void ConvencaoDeContagemDoPrazo_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<ConvencaoDeContagemDoPrazo>(TipoRegra.ConvencaoContagemPrazo);

    [Fact]
    public void RegraDePrazoDoRecurso_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDePrazoDoRecurso>(TipoRegra.RegraPrazoRecurso);

    [Fact]
    public void RegraDeApuracaoDaAvaliacao_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeApuracaoDaAvaliacao>(TipoRegra.RegraApuracaoAvaliacao);

    [Fact]
    public void CriterioDeRemanejamento_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<CriterioDeRemanejamento>(TipoRegra.CriterioRemanejamento);

    [Fact]
    public void RegraDeProcedimentoDoRecurso_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeProcedimentoDoRecurso>(TipoRegra.RegraProcedimentoRecurso);

    [Fact]
    public void RegraDeDistribuicaoDeVagas_SementeV1_ContemExatamenteOsCodigosDoEnum() =>
        Conferir<RegraDeDistribuicaoDeVagas>(TipoRegra.RegraDistribuicaoVagas);

    private static void Conferir<TRegra>(TipoRegra tipo)
        where TRegra : struct, Enum =>
        RegraCatalogoSeed.Itens.Where(item => item.Tipo == tipo && item.Versao == RegraCatalogoSeed.VersaoV1)
            .Select(static item => item.Codigo)
            .Should().BeEquivalentTo(CatalogoDe<TRegra>.Todos().Select(static item => item.Codigo));
}
