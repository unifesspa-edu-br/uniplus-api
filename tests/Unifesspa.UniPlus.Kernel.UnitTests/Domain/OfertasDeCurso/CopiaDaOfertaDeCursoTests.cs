namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.OfertasDeCurso;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CopiaDaOfertaDeCursoTests
{
    [Theory]
    [InlineData("OFERTA\nOUTRA")]
    [InlineData("oferta_invalida")]
    [InlineData(" X ")]
    [InlineData("A__B")]
    public void Criar_CodigoForaDoFormato_Recusa(string codigo)
    {
        Result<CopiaDaOfertaDeCurso> resultado = CopiaDaOfertaDeCurso.Criar(codigo, "Curso", "Grau", "Unidade", ["MATUTINO"]);

        resultado.Error!.Code.Should().Be(CopiaDaOfertaDeCursoErrorCodes.Invalida);
        resultado.Error.Message.Should().NotContain(codigo);
    }
    [Theory]
    [InlineData("A")]
    [InlineData("LEI_12711")]
    public void Criar_CodigoValido_Aceita(string codigo)
    {
        Result<CopiaDaOfertaDeCurso> resultado = CopiaDaOfertaDeCurso.Criar(codigo, "Curso", "Grau", "Unidade", ["MATUTINO"]);

        resultado.Value!.Codigo.Should().Be(codigo);
    }
}
