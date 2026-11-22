namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class OrigemDataFaseTests
{
    [Theory(DisplayName = "Cada origem da data da fase tem o código e o rótulo do contrato")]
    [InlineData(OrigemDataFase.NoEdital, "NO_EDITAL", "No edital")]
    [InlineData(OrigemDataFase.NaExecucao, "NA_EXECUCAO", "Na execução do processo")]
    public void Valor_Declarado_TemCodigoERotulo(OrigemDataFase origem, string codigo, string rotulo)
    {
        origem.Codigo().Should().Be(codigo);
        origem.Rotulo().Should().Be(rotulo);
        CatalogoDe<OrigemDataFase>.TentarObter(codigo, out OrigemDataFase lido).Should().BeTrue();
        lido.Should().Be(origem);
    }

    [Fact(DisplayName = "O enum tem exatamente as duas origens da data, na ordem de exibição")]
    public void Valores_Catalogo_DuasOrigensNaOrdemDeDeclaracao() =>
        CatalogoDe<OrigemDataFase>.Valores().Should().Equal(OrigemDataFase.NoEdital, OrigemDataFase.NaExecucao);

    [Theory(DisplayName = "Código fora do contrato não é origem da data, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" NO_EDITAL")]
    [InlineData("NO_EDITAL ")]
    [InlineData("no_edital")]
    [InlineData("NoEdital")]
    [InlineData("NENHUMA")]
    [InlineData("1")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<OrigemDataFase>.TentarObter(codigo, out _).Should().BeFalse();
}
