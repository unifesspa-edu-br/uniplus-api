namespace Unifesspa.UniPlus.SharedKernel.Tests.Domain.ValueObjects;

using FluentAssertions;

using Unifesspa.UniPlus.SharedKernel.Domain.ValueObjects;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed class CpfTests
{
    // ─── Factory — caminhos felizes ────────────────────────────────────────

    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("52998224725", "52998224725")]
    [InlineData("529.98224725", "52998224725")]
    [InlineData("529982247-25", "52998224725")]
    public void Criar_DadoCpfValidoEmDiferentesFormatos_DeveNormalizarParaApenasDigitos(string entrada, string esperado)
    {
        Result<Cpf> resultado = Cpf.Criar(entrada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(esperado);
    }

    // ─── Factory — validações ──────────────────────────────────────────────

    [Fact]
    public void Criar_DadoCpfNulo_DeveRetornarFailureCpfVazio()
    {
        Result<Cpf> resultado = Cpf.Criar(null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Cpf.Vazio");
    }

    [Fact]
    public void Criar_DadoStringVazia_DeveRetornarFailureCpfVazio()
    {
        Result<Cpf> resultado = Cpf.Criar(string.Empty);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Cpf.Vazio");
    }

    [Fact]
    public void Criar_DadoWhitespace_DeveRetornarFailureCpfVazio()
    {
        Result<Cpf> resultado = Cpf.Criar("   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Cpf.Vazio");
    }

    [Theory]
    [InlineData("123.456.789", "menos de 11 dígitos")]
    [InlineData("123.456.789-0123", "mais de 11 dígitos")]
    [InlineData("abcdefghijk", "sem dígitos suficientes após extração")]
    public void Criar_DadoCpfComQuantidadeDeDigitosInvalida_DeveRetornarFailure(string cpfInvalido, string razao)
    {
        Result<Cpf> resultado = Cpf.Criar(cpfInvalido);

        resultado.IsFailure.Should().BeTrue(razao);
        resultado.Error!.Code.Should().Be("Cpf.Invalido", razao);
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void Criar_DadoCpfComDigitosRepetidos_DeveRetornarFailure(string cpfInvalido)
    {
        Result<Cpf> resultado = Cpf.Criar(cpfInvalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Cpf.Invalido");
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("111.222.333-44")]
    public void Criar_DadoCpfComChecksumInvalido_DeveRetornarFailure(string cpfInvalido)
    {
        Result<Cpf> resultado = Cpf.Criar(cpfInvalido);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Cpf.Invalido");
    }

    // ─── Mascaramento (LGPD) ───────────────────────────────────────────────

    [Fact]
    public void Mascarado_DeveUsarHifenComoSeparadorDosDigitosVerificadores()
    {
        Cpf cpf = Cpf.Criar("529.982.247-25").Value!;

        cpf.Mascarado.Should().Be("***.***.***-25",
            "o padrão SERPRO/Gov.br exige hífen antes dos dois dígitos verificadores");
    }

    [Fact]
    public void Mascarado_DevePreservarApenasOsDoisUltimosDigitos()
    {
        Cpf cpf = Cpf.Criar("52998224725").Value!;

        cpf.Mascarado.Should().EndWith("-25");
        cpf.Mascarado.Should().NotContain("52998224", "dígitos intermediários não podem vazar");
    }

    [Fact]
    public void ToString_DeveRetornarFormatoMascarado()
    {
        Cpf cpf = Cpf.Criar("529.982.247-25").Value!;

        cpf.ToString().Should().Be(cpf.Mascarado);
        cpf.ToString().Should().Be("***.***.***-25");
    }

    // ─── Igualdade de record ───────────────────────────────────────────────

    [Fact]
    public void Cpf_DoisCpfsComMesmoValor_DevemSerIguais()
    {
        Cpf cpf1 = Cpf.Criar("529.982.247-25").Value!;
        Cpf cpf2 = Cpf.Criar("52998224725").Value!;

        cpf1.Should().Be(cpf2, "record compara por valor após normalização");
        (cpf1 == cpf2).Should().BeTrue();
        cpf1.GetHashCode().Should().Be(cpf2.GetHashCode());
    }
}
