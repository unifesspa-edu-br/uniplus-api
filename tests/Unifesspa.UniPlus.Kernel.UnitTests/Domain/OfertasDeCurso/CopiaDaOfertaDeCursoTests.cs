namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.OfertasDeCurso;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CopiaDaOfertaDeCursoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("oferta")]
    [InlineData(" OFERTA")]
    [InlineData("OFERTA ")]
    [InlineData("A__B")]
    [InlineData("_OFERTA")]
    [InlineData("OFERTA_")]
    [InlineData("1OFERTA")]
    [InlineData("OFÉRTA")]
    [InlineData("OFERTA\nOUTRA")]
    public void Criar_CodigoForaDoFormato_RecusaNoCampo(string? codigo)
    {
        Result<CopiaDaOfertaDeCurso> resultado = Criar(codigo: codigo);
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("ofertaCursoCodigo");
        resultado.Error!.Code.Should().Be(CopiaDaOfertaDeCursoErrorCodes.CodigoFormatoInvalido);
        resultado.Error.Message.Should().Be("O código da oferta de curso deve começar com letra maiúscula e conter só letras maiúsculas sem acento, algarismos e sublinhado entre as palavras, com até 50 caracteres.");
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Criar_TamanhoDoCodigo_ConfereLimite(int tamanho, bool esperado)
    {
        string codigo = new('X', tamanho);
        Result<CopiaDaOfertaDeCurso> resultado = Criar(codigo: codigo);
        resultado.IsSuccess.Should().Be(esperado);
        if (esperado)
        {
            resultado.Value!.Codigo.Should().Be(codigo);
        }
        else
        {
            resultado.Error!.Code.Should().Be(CopiaDaOfertaDeCursoErrorCodes.CodigoFormatoInvalido);
        }
    }

    [Theory]
    [InlineData("cursoNome", 200)]
    [InlineData("cursoGrau", 60)]
    [InlineData("unidadeOfertanteNome", 250)]
    [InlineData("turnos", 30)]
    public void Criar_TamanhoDosTextos_ConfereCadaLimite(string campo, int limite)
    {
        CriarComTexto(campo, new string('X', limite)).IsSuccess.Should().BeTrue();
        Result<CopiaDaOfertaDeCurso> resultado = CriarComTexto(campo, new string('X', limite + 1));
        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be(campo);
        resultado.Error!.Code.Should().Be(CopiaDaOfertaDeCursoErrorCodes.Invalida);
        resultado.Error.Message.Should().NotContain(new string('X', limite + 1));
    }

    [Fact]
    public void Criar_TentativaDeAlterarTurnos_PreservaCopia()
    {
        List<string> turnos = ["MATUTINO"];
        CopiaDaOfertaDeCurso oferta = CopiaDaOfertaDeCurso.Criar("OFERTA", "Curso", "Grau", "Unidade", turnos).Value!;
        turnos[0] = "NOTURNO";

        IList<string> turnosDaCopia = (IList<string>)oferta.Turnos;
        Action alterar = () => turnosDaCopia[0] = "VESPERTINO";
        alterar.Should().Throw<NotSupportedException>();
        oferta.Turnos.Should().Equal("MATUTINO");
    }

    [Fact]
    public void Criar_TextosComEspacos_AparaTextosEPreservaCodigo()
    {
        CopiaDaOfertaDeCurso oferta = CopiaDaOfertaDeCurso.Criar("OFERTA_1", " Curso ", " Grau ", " Unidade ", [" MATUTINO "]).Value!;
        oferta.Codigo.Should().Be("OFERTA_1");
        oferta.CursoNome.Should().Be("Curso");
        oferta.CursoGrau.Should().Be("Grau");
        oferta.UnidadeOfertanteNome.Should().Be("Unidade");
        oferta.Turnos.Should().Equal("MATUTINO");
    }

    [Fact]
    public void Criar_VariosCamposInvalidos_AcumulaNaOrdemSemRepetirValores()
    {
        string[] valores = ["codigo!", new('N', 201), new('G', 61), new('U', 251), new('T', 31)];
        Result<CopiaDaOfertaDeCurso> resultado = CopiaDaOfertaDeCurso.Criar(valores[0], valores[1], valores[2], valores[3], [valores[4]]);
        resultado.Errors.Select(static erro => erro.Field).Should().Equal("ofertaCursoCodigo", "cursoNome", "cursoGrau", "unidadeOfertanteNome", "turnos");
        resultado.Errors.Select(static erro => erro.Error.Code).Should().Equal(CopiaDaOfertaDeCursoErrorCodes.CodigoFormatoInvalido,
            CopiaDaOfertaDeCursoErrorCodes.Invalida, CopiaDaOfertaDeCursoErrorCodes.Invalida, CopiaDaOfertaDeCursoErrorCodes.Invalida, CopiaDaOfertaDeCursoErrorCodes.Invalida);
        foreach (FieldError erro in resultado.Errors)
        {
            foreach (string valor in valores)
            {
                erro.Error.Message.Should().NotContain(valor);
            }
        }
    }

    private static Result<CopiaDaOfertaDeCurso> Criar(string? codigo) =>
        CopiaDaOfertaDeCurso.Criar(codigo, "Curso", "Grau", "Unidade", ["MATUTINO"]);

    private static Result<CopiaDaOfertaDeCurso> CriarComTexto(string campo, string texto) =>
        CopiaDaOfertaDeCurso.Criar("OFERTA", campo == "cursoNome" ? texto : "Curso", campo == "cursoGrau" ? texto : "Grau",
            campo == "unidadeOfertanteNome" ? texto : "Unidade", [campo == "turnos" ? texto : "MATUTINO"]);
}
