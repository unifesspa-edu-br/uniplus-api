namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RegraEliminacaoTests
{
    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.De(codigo, "v1", new string('a', 64));

    [Fact(DisplayName = "Criar ELIM-NOTA-MINIMA-ETAPA com parâmetros compatíveis tem sucesso")]
    public void Criar_NotaMinimaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa), new ParametrosElimNotaMinimaEtapa(etapaId, 4m));

        resultado.IsSuccess.Should().BeTrue();
        ((ParametrosElimNotaMinimaEtapa)resultado.Value!.Parametros).EtapaRef.Should().Be(etapaId);
    }

    [Theory(DisplayName = "Criar ELIM-CORTE-EM-AREA com área e mínimo válidos tem sucesso, em qualquer área")]
    [InlineData("REDACAO")]
    [InlineData("MATEMATICA")]
    public void Criar_CorteEmArea_Sucesso(string area)
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteEmArea), new ParametrosElimCorteEmArea(area, 400m));

        resultado.IsSuccess.Should().BeTrue();
        ((ParametrosElimCorteEmArea)resultado.Value!.Parametros).AreaCodigo.Should().Be(area);
    }

    [Theory(DisplayName = "Criar ELIM-CORTE-EM-AREA com código de área mal formado falha")]
    [InlineData("")]
    [InlineData("redacao")]
    [InlineData("Redação")]
    [InlineData("AREA COM ESPACO")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Criar_CorteEmAreaComAreaMalFormada_Falha(string area)
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteEmArea), new ParametrosElimCorteEmArea(area, 400m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.AreaInvalida");
    }

    [Fact(DisplayName = "Criar ELIM-ZERO-EM-AREA (sem parâmetros) tem sucesso")]
    public void Criar_ZeroEmArea_Sucesso()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimZeroEmArea), new ParametrosElimZeroEmArea());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar ELIM-FALTA-EM-DIA-DE-PROVA-ENEM (sem parâmetros) tem sucesso")]
    public void Criar_FaltaEmDiaDeProvaEnem_Sucesso()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem), new ParametrosElimFaltaEmDiaDeProvaEnem());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar ELIM-AUSENCIA-NA-ETAPA com a etapa citada tem sucesso")]
    public void Criar_AusenciaNaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();

        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa), new ParametrosElimAusenciaNaEtapa(etapaId));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Parametros.EtapaReferenciada().Should().Be(etapaId);
    }

    [Fact(DisplayName = "Parametros de nota mínima não servem à ELIM-AUSENCIA-NA-ETAPA")]
    public void Criar_AusenciaNaEtapaComParametrosDeNotaMinima_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimAusenciaNaEtapa), new ParametrosElimNotaMinimaEtapa(Guid.CreateVersion7(), 4m));

        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    [Theory(DisplayName = "Falta em dia de prova e zero em área não trocam de parâmetros entre si")]
    [InlineData(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, false)]
    [InlineData(RegraEliminacaoCodigo.ElimZeroEmArea, true)]
    public void Criar_ParametrosSemCamposDeOutraRegra_Falha(string codigo, bool parametrosDeFalta)
    {
        ParametrosRegraEliminacao parametros = parametrosDeFalta ? new ParametrosElimFaltaEmDiaDeProvaEnem() : new ParametrosElimZeroEmArea();

        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(Regra(codigo), parametros);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    public static TheoryData<ParametrosRegraEliminacao, bool> ExigenciaDeEnemPorVariante => new()
    {
        { new ParametrosElimNotaMinimaEtapa(Guid.CreateVersion7(), 4m), false },
        { new ParametrosElimCorteEmArea("REDACAO", 400m), true },
        { new ParametrosElimZeroEmArea(), true },
        { new ParametrosElimFaltaEmDiaDeProvaEnem(), true },
        { new ParametrosElimAusenciaNaEtapa(Guid.CreateVersion7()), false },
    };

    [Theory(DisplayName = "Cada variante declara se exige classificação baseada em ENEM")]
    [MemberData(nameof(ExigenciaDeEnemPorVariante))]
    public void ExigeEnem_PorVariante(ParametrosRegraEliminacao parametros, bool exigeEnem)
    {
        ArgumentNullException.ThrowIfNull(parametros);

        parametros.ExigeEnem().Should().Be(exigeEnem);
    }

    [Fact(DisplayName = "Criar com parâmetros incompatíveis com a regra falha")]
    public void Criar_ParametrosIncompativeis_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa), new ParametrosElimZeroEmArea());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    [Fact(DisplayName = "Criar ELIM-NOTA-MINIMA-ETAPA com nota mínima negativa falha")]
    public void Criar_NotaMinimaNegativa_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimNotaMinimaEtapa), new ParametrosElimNotaMinimaEtapa(Guid.CreateVersion7(), -1m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.NotaMinimaInvalida");
    }

    [Fact(DisplayName = "Criar ELIM-CORTE-EM-AREA com mínimo negativo falha")]
    public void Criar_MinimoNegativo_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraEliminacaoCodigo.ElimCorteEmArea), new ParametrosElimCorteEmArea("REDACAO", -1m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.MinimoInvalido");
    }
}
