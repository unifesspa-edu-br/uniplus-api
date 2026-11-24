namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class RegraEliminacaoTests
{
    private static ReferenciaRegra Regra(RegraDeEliminacaoPorCalculo regra) => Regra(regra.Codigo());

    private static ReferenciaRegra Regra(string codigo) =>
        ReferenciaRegra.De(codigo, "v1", new string('a', 64));

    [Fact(DisplayName = "Criar ELIMINACAO_NOTA_MINIMA_ETAPA com parâmetros compatíveis tem sucesso")]
    public void Criar_NotaMinimaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa), new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaId, 4m));

        resultado.IsSuccess.Should().BeTrue();
        ((ParametrosEliminacaoPorNotaMinimaNaEtapa)resultado.Value!.Parametros).EtapaRef.Should().Be(etapaId);
    }

    [Theory(DisplayName = "Criar ELIMINACAO_CORTE_EM_AREA com área e mínimo válidos tem sucesso, em qualquer área")]
    [InlineData("REDACAO")]
    [InlineData("MATEMATICA")]
    public void Criar_CorteEmArea_Sucesso(string area)
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem), new ParametrosEliminacaoPorCorteEmAreaDoEnem(area, 400m));

        resultado.IsSuccess.Should().BeTrue();
        ((ParametrosEliminacaoPorCorteEmAreaDoEnem)resultado.Value!.Parametros).AreaCodigo.Should().Be(area);
    }

    [Theory(DisplayName = "Criar ELIMINACAO_CORTE_EM_AREA com código de área mal formado falha")]
    [InlineData("")]
    [InlineData("redacao")]
    [InlineData("Redação")]
    [InlineData("AREA COM ESPACO")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Criar_CorteEmAreaComAreaMalFormada_Falha(string area)
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem), new ParametrosEliminacaoPorCorteEmAreaDoEnem(area, 400m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.AreaInvalida");
    }

    [Fact(DisplayName = "Criar ELIMINACAO_ZERO_EM_AREA (sem parâmetros) tem sucesso")]
    public void Criar_ZeroEmArea_Sucesso()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem), new ParametrosEliminacaoPorZeroEmAreaDoEnem());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM (sem parâmetros) tem sucesso")]
    public void Criar_FaltaEmDiaDeProvaEnem_Sucesso()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem), new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem());

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar ELIMINACAO_AUSENCIA_NA_ETAPA com a etapa citada tem sucesso")]
    public void Criar_AusenciaNaEtapa_Sucesso()
    {
        Guid etapaId = Guid.CreateVersion7();

        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa), new ParametrosEliminacaoPorAusenciaNaEtapa(etapaId));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Parametros.EtapaReferenciada().Should().Be(etapaId);
    }

    [Fact(DisplayName = "Parametros de nota mínima não servem à ELIMINACAO_AUSENCIA_NA_ETAPA")]
    public void Criar_AusenciaNaEtapaComParametrosDeNotaMinima_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa), new ParametrosEliminacaoPorNotaMinimaNaEtapa(Guid.CreateVersion7(), 4m));

        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    [Theory(DisplayName = "Falta em dia de prova e zero em área não trocam de parâmetros entre si")]
    [InlineData(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem, false)]
    [InlineData(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem, true)]
    public void Criar_ParametrosSemCamposDeOutraRegra_Falha(RegraDeEliminacaoPorCalculo codigo, bool parametrosDeFalta)
    {
        ParametrosRegraEliminacao parametros = parametrosDeFalta ? new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem() : new ParametrosEliminacaoPorZeroEmAreaDoEnem();

        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(Regra(codigo), parametros);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    public static TheoryData<ParametrosRegraEliminacao, bool> ExigenciaDeEnemPorVariante => new()
    {
        { new ParametrosEliminacaoPorNotaMinimaNaEtapa(Guid.CreateVersion7(), 4m), false },
        { new ParametrosEliminacaoPorCorteEmAreaDoEnem("REDACAO", 400m), true },
        { new ParametrosEliminacaoPorZeroEmAreaDoEnem(), true },
        { new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem(), true },
        { new ParametrosEliminacaoPorAusenciaNaEtapa(Guid.CreateVersion7()), false },
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
            Regra(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa), new ParametrosEliminacaoPorZeroEmAreaDoEnem());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }

    [Fact(DisplayName = "Criar ELIMINACAO_NOTA_MINIMA_ETAPA com nota mínima negativa falha")]
    public void Criar_NotaMinimaNegativa_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa), new ParametrosEliminacaoPorNotaMinimaNaEtapa(Guid.CreateVersion7(), -1m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.NotaMinimaInvalida");
    }

    [Fact(DisplayName = "Criar ELIMINACAO_CORTE_EM_AREA com mínimo negativo falha")]
    public void Criar_MinimoNegativo_Falha()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem), new ParametrosEliminacaoPorCorteEmAreaDoEnem("REDACAO", -1m));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("RegraEliminacao.MinimoInvalido");
    }
    [Fact]
    public void Criar_RegraDesconhecida_RecusaParametros()
    {
        Result<RegraEliminacao> resultado = RegraEliminacao.Criar(
            Regra("ELIMINACAO-INEXISTENTE"), new ParametrosEliminacaoPorZeroEmAreaDoEnem());

        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
    }
}
