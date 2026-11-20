namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class NomeSocialValueConverterTests
{
    private readonly NomeSocialValueConverter _conversor = new();

    [Theory(DisplayName = "A escrita grava as chaves em camelCase e mantém o nome social ausente como nulo")]
    [InlineData("Pessoa de Teste", null, """{"nomeCivil":"Pessoa de Teste","nome":null}""")]
    [InlineData("Pessoa de Teste", "Nome de Teste", """{"nomeCivil":"Pessoa de Teste","nome":"Nome de Teste"}""")]
    public void ConvertToProvider_NomeSocial_GravaOsDoisCampos(string nomeCivil, string? nomeSocial, string documentoEsperado) =>
        _conversor.ConvertToProvider(NomeSocial.Criar(nomeCivil, nomeSocial).Value!).Should().Be(documentoEsperado);

    [Theory(DisplayName = "A leitura devolve o nome social que foi gravado")]
    [InlineData("Pessoa de Teste", null)]
    [InlineData("Pessoa de Teste", "Nome de Teste")]
    public void ConvertFromProvider_DocumentoGravado_DevolveOMesmoNomeSocial(string nomeCivil, string? nomeSocial)
    {
        NomeSocial original = NomeSocial.Criar(nomeCivil, nomeSocial).Value!;

        object? lido = _conversor.ConvertFromProvider(_conversor.ConvertToProvider(original));

        lido.Should().Be(original);
    }
}
