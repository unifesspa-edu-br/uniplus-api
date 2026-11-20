namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Apoio;

public sealed class CodigoDeContratoValueConverterTests
{
    private readonly CodigoDeContratoValueConverter<FormatoDeAula> _conversor = new();

    [Theory(DisplayName = "A escrita devolve o código declarado no membro, e não o nome dele")]
    [InlineData(FormatoDeAula.Presencial, "em_sala")]
    [InlineData(FormatoDeAula.Remoto, "a_distancia")]
    public void ConvertToProvider_EnumComCodigo_DevolveOCodigoDeclarado(FormatoDeAula formato, string codigoEsperado) =>
        _conversor.ConvertToProvider(formato).Should().Be(codigoEsperado);

    [Theory(DisplayName = "A leitura devolve o valor do código declarado")]
    [InlineData("em_sala", FormatoDeAula.Presencial)]
    [InlineData("a_distancia", FormatoDeAula.Remoto)]
    public void ConvertFromProvider_CodigoDeclarado_DevolveOValor(string codigo, FormatoDeAula formatoEsperado) =>
        _conversor.ConvertFromProvider(codigo).Should().Be(formatoEsperado);

    [Theory(DisplayName = "A leitura de código que o enum não declara falha, sem valor substituto")]
    [InlineData("inexistente")]
    [InlineData("Presencial")]
    [InlineData("EM_SALA")]
    [InlineData(" em_sala")]
    [InlineData("")]
    public void ConvertFromProvider_CodigoQueOEnumNaoDeclara_Lanca(string codigo) =>
        FluentActions.Invoking(() => _conversor.ConvertFromProvider(codigo))
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
