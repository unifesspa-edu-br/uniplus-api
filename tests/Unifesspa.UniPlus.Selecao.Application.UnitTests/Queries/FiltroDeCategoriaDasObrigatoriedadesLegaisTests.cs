namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class FiltroDeCategoriaDasObrigatoriedadesLegaisTests
{
    [Theory(DisplayName = "Sem categoria informada, a consulta não filtra")]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_SemCategoria_NaoFiltra(string? categoria)
    {
        Result<CategoriaObrigatoriedade?> resultado = FiltroDeCategoriaDasObrigatoriedadesLegais.Ler(categoria);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact(DisplayName = "O código publicado da categoria vira o filtro")]
    public void Ler_CodigoPublicado_Filtra()
    {
        Result<CategoriaObrigatoriedade?> resultado = FiltroDeCategoriaDasObrigatoriedadesLegais.Ler("ETAPA");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(CategoriaObrigatoriedade.Etapa);
    }

    [Theory(DisplayName = "Código que o vocabulário não conhece é recusado sem repetir o valor recebido")]
    [InlineData("Etapa")]
    [InlineData("etapa")]
    [InlineData("   ")]
    [InlineData(" ETAPA ")]
    [InlineData("etapa ")]
    [InlineData("inexistente")]
    public void Ler_CodigoDesconhecido_Recusa(string categoria)
    {
        Result<CategoriaObrigatoriedade?> resultado = FiltroDeCategoriaDasObrigatoriedadesLegais.Ler(categoria);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FiltroDeCategoriaDasObrigatoriedadesLegais.CodigoDeErro);
        resultado.Error.Message.Should().NotContain(categoria);
    }
}
