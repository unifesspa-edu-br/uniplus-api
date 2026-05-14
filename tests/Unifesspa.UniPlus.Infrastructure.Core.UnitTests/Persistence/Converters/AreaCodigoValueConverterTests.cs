namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence.Converters;

using AwesomeAssertions;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

public sealed class AreaCodigoValueConverterTests
{
    private readonly AreaCodigoValueConverter _converter = new();

    [Fact]
    public void ConvertToProvider_DadoAreaCodigoValido_DeveRetornarAStringSubjacente()
    {
        AreaCodigo codigo = AreaCodigo.From("CEPS").Value!;

        object? provider = _converter.ConvertToProvider(codigo);

        provider.Should().Be("CEPS");
    }

    [Fact]
    public void ConvertToProvider_DadoDefaultAreaCodigo_DeveLancarInvalidOperationException()
    {
        // default(AreaCodigo) tem Value null — persistir como NULL ficaria
        // indistinguível de "sem proprietário" numa coluna AreaCodigo?.
        // O converter falha alto na escrita.
        Action converter = () => _converter.ConvertToProvider(default(AreaCodigo));

        converter.Should().Throw<InvalidOperationException>()
            .WithMessage("*default(AreaCodigo)*");
    }

    [Fact]
    public void ConvertFromProvider_DadoStringValida_DeveReidratarOAreaCodigo()
    {
        object? codigo = _converter.ConvertFromProvider("PLATAFORMA");

        codigo.Should().Be(AreaCodigo.From("PLATAFORMA").Value!);
    }

    [Fact]
    public void ConvertFromProvider_DadoStringCorrompida_DeveLancarInvalidOperationExceptionComContexto()
    {
        Action converter = () => _converter.ConvertFromProvider("1-invalido");

        converter.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains(nameof(AreaCodigo), StringComparison.Ordinal)
                     && e.Message.Contains(AreaCodigo.CodigoErroInvalido, StringComparison.Ordinal));
    }
}
