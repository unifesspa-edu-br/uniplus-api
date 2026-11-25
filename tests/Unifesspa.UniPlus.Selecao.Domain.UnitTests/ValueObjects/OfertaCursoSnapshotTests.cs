namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class OfertaCursoSnapshotTests
{
    [Theory]
    [InlineData("OFERTA\nOUTRA")]
    [InlineData("oferta_invalida")]
    [InlineData(" X ")]
    [InlineData("A__B")]
    public void Criar_CodigoForaDoFormato_Recusa(string codigo)
    {
        Result<OfertaCursoSnapshot> resultado = OfertaCursoSnapshot.Criar(codigo, "Curso", "Grau", "Unidade", ["MATUTINO"]);

        resultado.Error!.Code.Should().Be("OfertaCursoSnapshot.Invalida");
        resultado.Error.Message.Should().NotContain(codigo);
    }
    [Theory]
    [InlineData("A")]
    [InlineData("LEI_12711")]
    public void Criar_CodigoValido_Aceita(string codigo)
    {
        Result<OfertaCursoSnapshot> resultado = OfertaCursoSnapshot.Criar(codigo, "Curso", "Grau", "Unidade", ["MATUTINO"]);

        resultado.Value!.Codigo.Should().Be(codigo);
    }
}
