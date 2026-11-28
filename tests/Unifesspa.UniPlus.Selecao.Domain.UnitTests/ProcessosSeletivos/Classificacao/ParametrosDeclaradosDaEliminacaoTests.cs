namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Classificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class ParametrosDeclaradosDaEliminacaoTests
{
    private static readonly Guid Etapa = Guid.NewGuid();

    private static Result<ParametrosRegraEliminacao> Montar(
        RegraDeEliminacaoPorCalculo regra, Guid? etapa = null, decimal? notaMinima = null, decimal? minimo = null, string? area = null) =>
        ParametrosDeclaradosDaEliminacao.Montar(regra.Codigo(), etapa, notaMinima, minimo, area);

    [Fact(DisplayName = "Cada regra monta os parâmetros que exige")]
    public void RegraComParametrosCompletos_MontaOsParametros()
    {
        Montar(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa, etapa: Etapa, notaMinima: 4m).Value.Should().Be(new ParametrosEliminacaoPorNotaMinimaNaEtapa(Etapa, 4m));
        Montar(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem, minimo: 400m, area: "REDACAO").Value.Should().Be(new ParametrosEliminacaoPorCorteEmAreaDoEnem("REDACAO", 400m));
        Montar(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa, etapa: Etapa).Value.Should().Be(new ParametrosEliminacaoPorAusenciaNaEtapa(Etapa));
        Montar(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem).Value.Should().Be(new ParametrosEliminacaoPorZeroEmAreaDoEnem());
        Montar(RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem).Value.Should().Be(new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem());
    }

    [Fact(DisplayName = "Regra sem parâmetros recusa qualquer parâmetro e diz para retirá-los")]
    public void RegraSemParametrosComParametro_Recusa()
    {
        Result<ParametrosRegraEliminacao> resultado = Montar(RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem, etapa: Etapa);

        resultado.Error!.Code.Should().Be("RegraEliminacao.ParametrosIncompativeisComRegra");
        resultado.Error.Message.Should().Be("A regra de eliminação não aceita parâmetros. Retire os parâmetros da regra.");
    }

    [Theory(DisplayName = "Parâmetro que falta ou sobra recusa com o código da forma da regra e diz o que informar e o que retirar")]
    [InlineData(RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa, "RegraEliminacao.EtapaRefENotaMinimaObrigatorios",
        "A regra de eliminação por nota mínima exige a etapa e a nota mínima. Informe os dois e retire o mínimo e a área.")]
    [InlineData(RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem, "RegraEliminacao.AreaEMinimoObrigatorios",
        "A regra de eliminação por corte em área exige a área e o mínimo. Informe os dois e retire a etapa e a nota mínima.")]
    [InlineData(RegraDeEliminacaoPorCalculo.AusenciaNaEtapa, "RegraEliminacao.EtapaRefObrigatorio",
        "A regra de eliminação por ausência exige a etapa. Informe a etapa e retire a nota mínima, o mínimo e a área.")]
    public void ParametrosAusentes_Recusam(RegraDeEliminacaoPorCalculo regra, string codigoEsperado, string mensagemEsperada)
    {
        Result<ParametrosRegraEliminacao> resultado = Montar(regra);

        resultado.Error!.Code.Should().Be(codigoEsperado);
        resultado.Error.Message.Should().Be(mensagemEsperada);
    }

    [Fact(DisplayName = "Código de regra que o sistema não conhece recusa como tipo inválido, sem repetir o código")]
    public void CodigoDesconhecido_Recusa()
    {
        Result<ParametrosRegraEliminacao> resultado = ParametrosDeclaradosDaEliminacao.Montar("CODIGO-DESCONHECIDO", null, null, null, null);

        resultado.Error!.Code.Should().Be(ParametrosDeclaradosDaEliminacao.CodigoDeRegraDesconhecido);
        resultado.Error.Message.Should().Be("O código da regra de eliminação não é conhecido.");
    }
}
