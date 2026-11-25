namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

/// <summary>
/// As regras de eliminação da classificação: cada uma grava os parâmetros que a regra declara e recusa
/// o payload com parâmetro faltando, sobrando ou contraditório.
/// </summary>
public sealed class DefinirClassificacaoRegrasDeEliminacaoCommandHandlerTests
{
    private static CenarioDaClassificacao CenarioComEliminacao(ProcessoSeletivo processo, string codigoDaRegra) =>
        Com(processo).ComRegrasBasicas().ComRegra(codigoDaRegra, TipoRegra.RegraEliminacao);

    [Fact(DisplayName = "Handle com ELIMINACAO_NOTA_MINIMA_ETAPA resolve parâmetros e persiste")]
    public async Task Handle_ComEliminacaoNotaMinimaEtapa_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PS Convênios 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        EtapaProcesso etapa = EtapaObjetiva();
        processo.DefinirEtapas([etapa]);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo());

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo(), "v1", etapa.Id, 4m, null, null)));

        result.IsSuccess.Should().BeTrue();
        RegraEliminacao eliminacao = processo.Classificacao!.RegrasEliminacao.Single();
        ((ParametrosEliminacaoPorNotaMinimaNaEtapa)eliminacao.Parametros).EtapaId.Should().Be(etapa.Id);
    }

    [Fact(DisplayName = "Handle com ELIMINACAO_NOTA_MINIMA_ETAPA sem EtapaRef/NotaMinima recusa")]
    public async Task Handle_EliminacaoSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo());

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo(), "v1", null, null, null, null)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.EtapaRefENotaMinimaObrigatorios");
    }

    [Fact(DisplayName = "Handle com ELIMINACAO_AUSENCIA_NA_ETAPA e a etapa citada grava a regra")]
    public async Task Handle_AusenciaNaEtapa_GravaARegra()
    {
        ProcessoSeletivo processo = NovoProcesso("PS Convênios 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        EtapaProcesso etapa = EtapaObjetiva();
        processo.DefinirEtapas([etapa]);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.AusenciaNaEtapa.Codigo());

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa.Codigo(), "v1", etapa.Id, null, null, null)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.RegrasEliminacao.Single().Parametros.Should().Be(new ParametrosEliminacaoPorAusenciaNaEtapa(etapa.Id));
    }

    [Theory(DisplayName = "Handle com ELIMINACAO_AUSENCIA_NA_ETAPA sem EtapaRef ou com arg alheio recusa")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Handle_AusenciaNaEtapaComParametrosErrados_Recusa(bool comEtapaRef, bool comNotaMinima)
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.AusenciaNaEtapa.Codigo());
        RegraEliminacaoInput eliminacao = new(
            RegraDeEliminacaoPorCalculo.AusenciaNaEtapa.Codigo(), "v1", comEtapaRef ? Guid.CreateVersion7() : null, comNotaMinima ? 4m : null, null, null);

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(processo.Id, eliminacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.EtapaRefObrigatorio");
    }

    [Fact(DisplayName = "Handle com ELIMINACAO_ZERO_EM_AREA e Minimo estranho aos parâmetros recusa (payload contraditório)")]
    public async Task Handle_ZeroEmAreaComParametrosEstranhos_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("SiSU 2026", TiposDeProcessoDeTeste.Sisu);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem.Codigo(), "v1", null, null, 400m, null)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    [Theory(DisplayName = "Handle com ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM e qualquer arg recusa: a regra não tem parâmetro")]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public async Task Handle_FaltaEmDiaDeProvaEnemComParametros_Recusa(bool comEtapaRef, bool comNotaMinima, bool comMinimo, bool comArea)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo());
        RegraEliminacaoInput eliminacao = new(
            RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo(), "v1",
            comEtapaRef ? Guid.CreateVersion7() : null,
            comNotaMinima ? 4m : null,
            comMinimo ? 400m : null,
            comArea ? "REDACAO" : null);

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(processo.Id, eliminacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    [Fact(DisplayName = "Handle com ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM sem parâmetros em processo ENEM grava a regra")]
    public async Task Handle_FaltaEmDiaDeProvaEnemSemParametros_GravaARegra()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo()).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem.Codigo(), "v1", null, null, null, null)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.RegrasEliminacao.Should().ContainSingle()
            .Which.Parametros.Should().BeOfType<ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem>();
    }

    [Fact(DisplayName = "Handle com ELIMINACAO_CORTE_EM_AREA grava a área e o mínimo")]
    public async Task Handle_CorteEmArea_GravaAreaEMinimo()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo()).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo(), "v1", null, null, 450m, "MATEMATICA")));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.RegrasEliminacao.Should().ContainSingle()
            .Which.Parametros.Should().Be(new ParametrosEliminacaoPorCorteEmAreaDoEnem("MATEMATICA", 450m));
    }

    [Theory(DisplayName = "Handle com ELIMINACAO_CORTE_EM_AREA sem área ou sem mínimo, ou com arg de outra regra, recusa")]
    [InlineData(null, 400.0, false)]
    [InlineData("REDACAO", null, false)]
    [InlineData("REDACAO", 400.0, true)]
    public async Task Handle_CorteEmAreaIncompleto_Recusa(string? area, double? minimo, bool comEtapaRef)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo());
        RegraEliminacaoInput eliminacao = new(
            RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo(), "v1", comEtapaRef ? Guid.CreateVersion7() : null, null, (decimal?)minimo, area);

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(processo.Id, eliminacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.AreaEMinimoObrigatorios");
    }

    [Fact(DisplayName = "Corte em área fora do quadro da resolução sai no campo da área do item")]
    public async Task Handle_CorteEmAreaForaDoQuadro_RecusaNaArea()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo()).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo(), "v1", null, null, 400m, "FISICA")));

        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ((string?)"regrasEliminacao[0].areaCodigo", "ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro"));
        processo.Classificacao.Should().BeNull();
    }
}
