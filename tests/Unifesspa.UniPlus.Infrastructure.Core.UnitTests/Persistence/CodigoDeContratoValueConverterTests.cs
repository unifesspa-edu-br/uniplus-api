namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class CodigoDeContratoValueConverterTests
{
    private readonly CodigoDeContratoValueConverter<FormatoDeAula> _conversor = new();

    [Theory(DisplayName = "A escrita devolve o código declarado no membro, e não o nome dele")]
    [InlineData(FormatoDeAula.Presencial, "EM_SALA")]
    [InlineData(FormatoDeAula.Remoto, "A_DISTANCIA")]
    public void ConvertToProvider_EnumComCodigo_DevolveOCodigoDeclarado(FormatoDeAula formato, string codigoEsperado) =>
        _conversor.ConvertToProvider(formato).Should().Be(codigoEsperado);

    [Theory(DisplayName = "A leitura devolve o valor do código declarado")]
    [InlineData("EM_SALA", FormatoDeAula.Presencial)]
    [InlineData("A_DISTANCIA", FormatoDeAula.Remoto)]
    public void ConvertFromProvider_CodigoDeclarado_DevolveOValor(string codigo, FormatoDeAula formatoEsperado) =>
        _conversor.ConvertFromProvider(codigo).Should().Be(formatoEsperado);

    [Theory(DisplayName = "A leitura de código que o enum não declara falha, sem valor substituto e sem repetir o código")]
    [InlineData("inexistente")]
    [InlineData("Presencial")]
    [InlineData("HIBRIDO")]
    [InlineData(" EM_SALA")]
    public void ConvertFromProvider_CodigoQueOEnumNaoDeclara_Lanca(string codigo) =>
        FluentActions.Invoking(() => _conversor.ConvertFromProvider(codigo))
            .Should().Throw<ArgumentException>().Which.Message.Should().Contain(nameof(FormatoDeAula)).And.NotContain(codigo);

    [Fact(DisplayName = "A leitura de código vazio falha, sem valor substituto")]
    public void ConvertFromProvider_CodigoVazio_Lanca() =>
        FluentActions.Invoking(() => _conversor.ConvertFromProvider(string.Empty))
            .Should().Throw<ArgumentException>().Which.Message.Should().Contain(nameof(FormatoDeAula));

    [Fact(DisplayName = "A escrita de valor que não é membro do enum falha, sem código inventado")]
    public void ConvertToProvider_ValorForaDoEnum_Lanca() =>
        FluentActions.Invoking(() => _conversor.ConvertToProvider((FormatoDeAula)99))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact(DisplayName = "Nulo não passa pelo conversor: a coluna opcional usa o mesmo conversor e NULL continua ausência")]
    public void Nulo_NaoPassaPeloConversor()
    {
        _conversor.ConvertsNulls.Should().BeFalse();
        _conversor.ConvertToProvider(null).Should().BeNull();
        _conversor.ConvertFromProvider(null).Should().BeNull();
    }
}
