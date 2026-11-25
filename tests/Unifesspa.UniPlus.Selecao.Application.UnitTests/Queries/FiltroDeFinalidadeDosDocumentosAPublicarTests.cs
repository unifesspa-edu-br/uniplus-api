namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class FiltroDeFinalidadeDosDocumentosAPublicarTests
{
    [Theory(DisplayName = "Sem finalidade informada, a consulta não filtra")]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_SemFinalidade_NaoFiltra(string? finalidade)
    {
        Result<FinalidadeDoDocumento?> resultado = FiltroDeFinalidadeDosDocumentosAPublicar.Ler(finalidade);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "O código publicado da finalidade vira o filtro")]
    public void Ler_CodigoPublicado_Filtra()
    {
        Result<FinalidadeDoDocumento?> resultado = FiltroDeFinalidadeDosDocumentosAPublicar.Ler("ANEXO_DO_EDITAL");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(FinalidadeDoDocumento.AnexoDoEdital);
    }

    [Theory(DisplayName = "Código que o vocabulário não conhece é recusado sem repetir o valor recebido")]
    [InlineData("AnexoDoEdital")]
    [InlineData("   ")]
    [InlineData(" ANEXO_DO_EDITAL ")]
    [InlineData("anexo_do_edital")]
    [InlineData("inexistente")]
    public void Ler_CodigoDesconhecido_Recusa(string finalidade)
    {
        Result<FinalidadeDoDocumento?> resultado = FiltroDeFinalidadeDosDocumentosAPublicar.Ler(finalidade);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FiltroDeFinalidadeDosDocumentosAPublicar.CodigoDeErro);
        resultado.Error.Message.Should().NotContain(finalidade);
    }
}
