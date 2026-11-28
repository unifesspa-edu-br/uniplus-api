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
    [InlineData(FonteValoresFato.Municipios, TipoDeCampo.Municipio, true)]
    [InlineData(FonteValoresFato.Municipios, TipoDeCampo.EscolhaUnica, false)]
    [InlineData(FonteValoresFato.UnidadesFederativas, TipoDeCampo.Municipio, false)]
    [InlineData(FonteValoresFato.UnidadesFederativas, TipoDeCampo.EscolhaUnica, true)]
    public void Coerencia_MunicipioDoGeo_SoNoCampoDeMunicipio(FonteValoresFato fonte, TipoDeCampo tipo, bool coerente) =>
        (CoerenciaDoCampo.Validar("CAMPO", tipo, TipoDoDado.Opcao, QuantidadeDeValores.ValorUnico, fonte) is null).Should().Be(coerente);

    [Fact]
    public void Forma_CampoDeMunicipioSemAUf_Recusa() =>
        Conferir(TipoDeCampo.Municipio, []).Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.MunicipioSemUf);

    [Fact]
    public void Forma_MunicipiosDaUfEmCampoDeSelecao_Recusa() =>
        Conferir(TipoDeCampo.EscolhaUnica, [new MunicipiosDaUf("NATURALIDADE_UF")])
            .Select(static e => e.Error.Code).Should().Equal(ItemFormularioErrorCodes.RestricaoIncoerente);

    [Fact]
    public void Forma_CampoDeMunicipioComAUf_Aceita() =>
        Conferir(TipoDeCampo.Municipio, [new MunicipiosDaUf("NATURALIDADE_UF")]).Should().BeEmpty();

    private static List<FieldError> Conferir(TipoDeCampo tipo, IReadOnlyList<RestricaoValor> restricoes) =>
        FormaDoItem.Conferir(
            "NATURALIDADE_MUNICIPIO",
            1,
            new ApresentacaoDoCampo("Município de nascimento", tipo),
            formato: null,
            RegrasLidasDoCampo.De(new RegrasDoCampo(exibicao: null, Obrigatoriedade.Sempre, restricoes)));
}
