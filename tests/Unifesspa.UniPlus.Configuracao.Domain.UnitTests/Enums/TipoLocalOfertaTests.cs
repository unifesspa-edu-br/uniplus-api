namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public sealed class TipoLocalOfertaTests
{
    [Theory(DisplayName = "Cada tipo de local de oferta tem o código e o rótulo do contrato")]
    [InlineData(TipoLocalOferta.CampusSede, "CAMPUS_SEDE", "Campus sede")]
    [InlineData(TipoLocalOferta.CampusForaDeSede, "CAMPUS_FORA_DE_SEDE", "Campus fora de sede")]
    [InlineData(TipoLocalOferta.CursoForaDeSede, "CURSO_FORA_DE_SEDE", "Curso fora de sede")]
    [InlineData(TipoLocalOferta.PoloDeEducacaoADistancia, "POLO_DE_EDUCACAO_A_DISTANCIA", "Polo de educação a distância")]
    [InlineData(TipoLocalOferta.ConvenioDeInteriorizacao, "CONVENIO_DE_INTERIORIZACAO", "Convênio de interiorização")]
    [InlineData(TipoLocalOferta.Outro, "OUTRO", "Outro")]
    public void Valor_Declarado_TemCodigoERotulo(TipoLocalOferta tipo, string codigo, string rotulo)
    {
        tipo.Codigo().Should().Be(codigo);
        tipo.Rotulo().Should().Be(rotulo);
        CatalogoDe<TipoLocalOferta>.TentarObter(codigo, out TipoLocalOferta lido).Should().BeTrue();
        lido.Should().Be(tipo);
    }

    [Fact(DisplayName = "O enum tem exatamente os seis tipos de local de oferta, na ordem de exibição")]
    public void Valores_Catalogo_SeisTiposNaOrdemDeDeclaracao() =>
        CatalogoDe<TipoLocalOferta>.Valores().Should().Equal(
            TipoLocalOferta.CampusSede,
            TipoLocalOferta.CampusForaDeSede,
            TipoLocalOferta.CursoForaDeSede,
            TipoLocalOferta.PoloDeEducacaoADistancia,
            TipoLocalOferta.ConvenioDeInteriorizacao,
            TipoLocalOferta.Outro);

    [Theory(DisplayName = "Código fora do contrato não é tipo de local de oferta, sem tolerância a espaço nem a caixa")]
    [InlineData("")]
    [InlineData(" CAMPUS_SEDE")]
    [InlineData("CAMPUS_SEDE ")]
    [InlineData("campus_sede")]
    [InlineData("CampusSede")]
    [InlineData("NENHUM")]
    [InlineData("POLO_EAD")]
    [InlineData("0")]
    public void TentarObter_CodigoForaDoContrato_NaoEncontra(string codigo) =>
        CatalogoDe<TipoLocalOferta>.TentarObter(codigo, out _).Should().BeFalse();
}
