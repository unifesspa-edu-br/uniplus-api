namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ReferenciaRegraTests
{
    private static readonly string HashValido = new('a', 64);

    [Fact(DisplayName = "De guarda a identidade da regra, sem os espaços das pontas do código e da versão")]
    public void De_IdentidadeValida_GuardaOsTresCampos()
    {
        ReferenciaRegra referencia = ReferenciaRegra.De(" BONUS-MULTIPLICATIVO ", " v1 ", HashValido);

        referencia.Codigo.Should().Be("BONUS-MULTIPLICATIVO");
        referencia.Versao.Should().Be("v1");
        referencia.Hash.Should().Be(HashValido);
    }

    [Theory(DisplayName = "De recusa código e versão em branco com ArgumentException")]
    [InlineData("", "v1", "codigo")]
    [InlineData("  ", "v1", "codigo")]
    [InlineData("R", "", "versao")]
    [InlineData("R", "  ", "versao")]
    public void De_CodigoOuVersaoEmBranco_LancaArgumentException(string codigo, string versao, string parametro)
    {
        Action montar = () => ReferenciaRegra.De(codigo, versao, HashValido);

        montar.Should().Throw<ArgumentException>().WithParameterName(parametro);
    }

    [Theory(DisplayName = "De recusa hash fora do SHA-256 hexadecimal minúsculo com ArgumentException")]
    [InlineData("abc")]
    [InlineData("")]
    public void De_HashForaDoFormato_LancaArgumentException(string hash)
    {
        Action montar = () => ReferenciaRegra.De("R", "v1", hash);

        montar.Should().Throw<ArgumentException>().WithParameterName(nameof(hash));
    }

    [Fact(DisplayName = "De recusa hash em maiúsculas")]
    public void De_HashEmMaiusculas_LancaArgumentException()
    {
        Action montar = () => ReferenciaRegra.De("R", "v1", new string('A', 64));

        montar.Should().Throw<ArgumentException>().WithParameterName("hash");
    }
}
