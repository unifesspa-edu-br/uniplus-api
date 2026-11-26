namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

public sealed class CodigoDeCadastroValueConverterTests
{
    private readonly CodigoDeCadastroValueConverter _conversor = new();

    [Fact]
    public void Gravar_CodigoForaDoFormato_NaoAltera() =>
        _conversor.ConvertToProvider("inválido").Should().Be("inválido");

    [Fact]
    public void Ler_CodigoValido_DevolveIgual() =>
        _conversor.ConvertFromProvider("CURSO_123").Should().Be("CURSO_123");

    [Fact]
    public void Ler_CodigoForaDoFormato_LancaSemExporValor()
    {
        const string valor = "codigo-invalido";
        Action leitura = () => _conversor.ConvertFromProvider(valor);
        leitura.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(valor);
    }
}
