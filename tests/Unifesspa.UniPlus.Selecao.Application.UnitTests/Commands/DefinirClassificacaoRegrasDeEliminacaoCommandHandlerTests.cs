namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaClassificacao;

/// <summary>
/// As regras de eliminação da classificação: cada uma grava os argumentos que a regra declara e recusa
/// o payload com argumento faltando, sobrando ou contraditório.
/// </summary>
public sealed class DefinirClassificacaoRegrasDeEliminacaoCommandHandlerTests
{
    private static CenarioDaClassificacao CenarioComEliminacao(ProcessoSeletivo processo, string codigoDaRegra) =>
        Com(processo).ComRegrasBasicas().ComRegra(codigoDaRegra, TipoRegra.RegraEliminacao);

    [Fact(DisplayName = "Handle com ELIM-NOTA-MINIMA-ETAPA resolve args e persiste")]
    public async Task Handle_ComEliminacaoNotaMinimaEtapa_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PS Convênios 2026", TiposDeProcessoDeTeste.PSIQ);
        EtapaProcesso etapa = EtapaObjetiva();
        processo.DefinirEtapas([etapa]);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimNotaMinimaEtapa);

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", etapa.Id, 4m, null, null)));

        result.IsSuccess.Should().BeTrue();
        RegraEliminacao eliminacao = processo.Classificacao!.RegrasEliminacao.Single();
        ((ArgsElimNotaMinimaEtapa)eliminacao.Args).EtapaRef.Should().Be(etapa.Id);
    }

    [Fact(DisplayName = "Handle com ELIM-NOTA-MINIMA-ETAPA sem EtapaRef/NotaMinima recusa")]
    public async Task Handle_EliminacaoSemEtapaRef_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimNotaMinimaEtapa);

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", null, null, null, null)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.EtapaRefENotaMinimaObrigatorios");
    }

    [Fact(DisplayName = "Handle com ELIM-AUSENCIA-NA-ETAPA e a etapa citada grava a regra")]
    public async Task Handle_AusenciaNaEtapa_GravaARegra()
    {
        ProcessoSeletivo processo = NovoProcesso("PS Convênios 2026", TiposDeProcessoDeTeste.PSIQ);
        EtapaProcesso etapa = EtapaObjetiva();
        processo.DefinirEtapas([etapa]);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimAusenciaNaEtapa);

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "v1", etapa.Id, null, null, null)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.RegrasEliminacao.Single().Args.Should().Be(new ArgsElimAusenciaNaEtapa(etapa.Id));
    }

    [Theory(DisplayName = "Handle com ELIM-AUSENCIA-NA-ETAPA sem EtapaRef ou com arg alheio recusa")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Handle_AusenciaNaEtapaComArgsErrados_Recusa(bool comEtapaRef, bool comNotaMinima)
    {
        ProcessoSeletivo processo = NovoProcesso("PSIQ 2026", TiposDeProcessoDeTeste.PSIQ);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimAusenciaNaEtapa);
        RegraEliminacaoInput eliminacao = new(
            RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "v1", comEtapaRef ? Guid.CreateVersion7() : null, comNotaMinima ? 4m : null, null, null);

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(processo.Id, eliminacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.EtapaRefObrigatorio");
    }

    [Fact(DisplayName = "Handle com ELIM-ZERO-EM-AREA e Minimo estranho ao args recusa (payload contraditório)")]
    public async Task Handle_ZeroEmAreaComArgsEstranhos_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("SiSU 2026", TiposDeProcessoDeTeste.SiSU);
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimZeroEmArea);

        Result result = await cenario.DefinirAsync(ComandoComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimZeroEmArea, "v1", null, null, 400m, null)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.ArgsIncompativeisComRegra");
    }

    [Theory(DisplayName = "Handle com ELIM-FALTA-EM-DIA-DE-PROVA-ENEM e qualquer arg recusa: a regra não tem parâmetro")]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public async Task Handle_FaltaEmDiaDeProvaEnemComArgs_Recusa(bool comEtapaRef, bool comNotaMinima, bool comMinimo, bool comArea)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem);
        RegraEliminacaoInput eliminacao = new(
            RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v1",
            comEtapaRef ? Guid.CreateVersion7() : null,
            comNotaMinima ? 4m : null,
            comMinimo ? 400m : null,
            comArea ? "REDACAO" : null);

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(processo.Id, eliminacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.ArgsIncompativeisComRegra");
    }

    [Fact(DisplayName = "Handle com ELIM-FALTA-EM-DIA-DE-PROVA-ENEM sem args em processo ENEM grava a regra")]
    public async Task Handle_FaltaEmDiaDeProvaEnemSemArgs_GravaARegra()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, "v1", null, null, null, null)));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.RegrasEliminacao.Should().ContainSingle()
            .Which.Args.Should().BeOfType<ArgsElimFaltaEmDiaDeProvaEnem>();
    }

    [Fact(DisplayName = "Handle com ELIM-CORTE-EM-AREA grava a área e o mínimo")]
    public async Task Handle_CorteEmArea_GravaAreaEMinimo()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimCorteEmArea).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimCorteEmArea, "v1", null, null, 450m, "MATEMATICA")));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Classificacao!.RegrasEliminacao.Should().ContainSingle()
            .Which.Args.Should().Be(new ArgsElimCorteEmArea("MATEMATICA", 450m));
    }

    [Theory(DisplayName = "Handle com ELIM-CORTE-EM-AREA sem área ou sem mínimo, ou com arg de outra regra, recusa")]
    [InlineData(null, 400.0, false)]
    [InlineData("REDACAO", null, false)]
    [InlineData("REDACAO", 400.0, true)]
    public async Task Handle_CorteEmAreaIncompleto_Recusa(string? area, double? minimo, bool comEtapaRef)
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimCorteEmArea);
        RegraEliminacaoInput eliminacao = new(
            RegraEliminacaoCodigo.ElimCorteEmArea, "v1", comEtapaRef ? Guid.CreateVersion7() : null, null, (decimal?)minimo, area);

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(processo.Id, eliminacao));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("RegraEliminacao.AreaEMinimoObrigatorios");
    }

    [Fact(DisplayName = "Corte em área fora do quadro da resolução sai no campo da área do item")]
    public async Task Handle_CorteEmAreaForaDoQuadro_RecusaNaArea()
    {
        ProcessoSeletivo processo = NovoProcessoEnem();
        CenarioDaClassificacao cenario = CenarioComEliminacao(processo, RegraEliminacaoCodigo.ElimCorteEmArea).ComResolucaoDosPesos();

        Result result = await cenario.DefinirAsync(ComandoEnemLocalComEliminacao(
            processo.Id, new RegraEliminacaoInput(RegraEliminacaoCodigo.ElimCorteEmArea, "v1", null, null, 400m, "FISICA")));

        result.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ((string?)"regrasEliminacao[0].areaCodigo", "ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro"));
        processo.Classificacao.Should().BeNull();
    }
}
