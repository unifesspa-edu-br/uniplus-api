namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class FormatoDoCodigoDoCursoTests
{
    [Theory]
    [InlineData("A")]
    [InlineData("ENGENHARIA_CIVIL_2")]
    public void Criar_CodigoNoFormato_PreservaValor(string codigo)
    {
        Result<Curso> resultado = Curso.Criar(codigo, "Curso", "Grau", "Nível", null);
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Codigo.Should().Be(codigo);
    }

    [Fact]
    public void Criar_CodigoComSessentaCaracteres_Aceita()
    {
        string codigo = new('A', 60);
        Curso.Criar(codigo, "Curso", "Grau", "Nível", null).Value!.Codigo.Should().Be(codigo);
    }

    [Theory]
    [InlineData(" CURSO")]
    [InlineData("CURSO ")]
    [InlineData("medicina")]
    [InlineData("CURSÓ")]
    [InlineData("CURSO\u0301")]
    [InlineData("CURSO__NOVO")]
    [InlineData("2CURSO")]
    public void Criar_CodigoForaDoFormato_RecusaSemRepetirValor(string codigo)
    {
        Result<Curso> resultado = Curso.Criar(codigo, "Curso", "Grau", "Nível", null);
        resultado.Error!.Code.Should().Be(CursoErrorCodes.CodigoFormatoInvalido);
        resultado.Error.Message.Should().NotContain(codigo);
    }

    [Fact]
    public void Atualizar_CodigoInvalidoEOutrosCamposInvalidos_AcumulaNaOrdem()
    {
        Curso curso = Curso.Criar("CURSO", "Curso", "Grau", "Nível", null).Value!;
        Result resultado = curso.Atualizar("curso", "", "", "", "DESCONHECIDO");
        resultado.Errors.Select(erro => erro.Field).Should().Equal("codigo", "nome", "grau", "nivelEnsino", "grupoAreaEnem");
        resultado.Errors[0].Error.Code.Should().Be(CursoErrorCodes.CodigoFormatoInvalido);
        curso.Codigo.Should().Be("CURSO");
    }
}
