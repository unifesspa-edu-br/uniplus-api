namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O município do Geo é respondido no campo de município, entre os da UF respondida antes
/// (UNI-REQ-0145): a lista vem do Geo no cliente, e o item cita a UF pelos municípios da UF.
/// </summary>
public sealed class CampoDeMunicipioTests
{
    [Theory]
    [InlineData(DominioDoCatalogo.FonteGeoMunicipio, TipoRenderizacao.Municipio, true)]
    [InlineData(DominioDoCatalogo.FonteGeoMunicipio, TipoRenderizacao.SelecaoUnica, false)]
    [InlineData(DominioDoCatalogo.FonteGeoUf, TipoRenderizacao.Municipio, false)]
    [InlineData(DominioDoCatalogo.FonteGeoUf, TipoRenderizacao.SelecaoUnica, true)]
    public void Coerencia_MunicipioDoGeo_SoNoCampoDeMunicipio(string fonte, TipoRenderizacao tipo, bool coerente) =>
        (CoerenciaDoCampo.Validar("CAMPO", tipo, DominioDoCatalogo.Categorico, DominioDoCatalogo.Escalar, fonte) is null).Should().Be(coerente);

    [Fact]
    public void Forma_CampoDeMunicipioSemAUf_Recusa() =>
        Conferir(TipoRenderizacao.Municipio, []).Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.MunicipioSemUf);

    [Fact]
    public void Forma_MunicipiosDaUfEmCampoDeSelecao_Recusa() =>
        Conferir(TipoRenderizacao.SelecaoUnica, [new MunicipiosDaUf("NATURALIDADE_UF")])
            .Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.RestricaoIncoerente);

    [Fact]
    public void Forma_CampoDeMunicipioComAUf_Aceita() =>
        Conferir(TipoRenderizacao.Municipio, [new MunicipiosDaUf("NATURALIDADE_UF")]).Should().BeEmpty();

    private static List<FieldError> Conferir(TipoRenderizacao tipo, IReadOnlyList<RestricaoValor> restricoes) =>
        FormaDoItem.Conferir("NATURALIDADE_MUNICIPIO", 1, "Município de nascimento", tipo, null, null, [], Obrigatoriedade.Sempre, restricoes);
}
