namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

public sealed class CaminhoDoConjuntoDeDocumentosTests
{
    [Theory]
    [InlineData(null, "conjuntoDeDocumentos")]
    [InlineData("", "conjuntoDeDocumentos")]
    [InlineData("faseCodigo", "conjuntoDeDocumentos.faseCodigo")]
    [InlineData("[0].faseCodigo", "conjuntoDeDocumentos[0].faseCodigo")]
    [InlineData("conjuntoDeDocumentos.documentos[0]", "conjuntoDeDocumentos.documentos[0]")]
    public void NoConjuntoDeDocumentos_CausaRelativa_SituaNoConjunto(string? campo, string esperado)
    {
        FieldError causa = new(campo, new DomainError("codigo", "mensagem"));

        FieldError resultado = SelecaoDosDocumentosDoModelo.NoConjuntoDeDocumentos(causa);

        resultado.Field.Should().Be(esperado);
        resultado.Error.Should().Be(causa.Error);
    }
}
