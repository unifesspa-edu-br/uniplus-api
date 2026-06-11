namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

public sealed class SlugTests
{
    [Theory(DisplayName = "Slug válido retorna Success com valor preservado")]
    [InlineData("ceps")]
    [InlineData("ceps-unifesspa")]
    [InlineData("faculdade-de-ciencias")]
    [InlineData("a1b")]
    [InlineData("abc123")]
    public void From_ComSlugValido_DeveRetornarSuccess(string valor)
    {
        Result<Slug> resultado = Slug.From(valor);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Valor.Should().Be(valor);
    }

    [Theory(DisplayName = "Slug nulo ou vazio retorna SlugObrigatorio")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_ComSlugVazioOuNulo_DeveRetornarSlugObrigatorio(string? valor)
    {
        Result<Slug> resultado = Slug.From(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugObrigatorio);
    }

    [Fact(DisplayName = "Slug com 2 chars retorna SlugTamanho (abaixo do mínimo)")]
    public void From_ComSlugDe2Chars_DeveRetornarSlugTamanho()
    {
        Result<Slug> resultado = Slug.From("ab");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugTamanho);
    }

    [Fact(DisplayName = "Slug com 65 chars retorna SlugTamanho (acima do máximo)")]
    public void From_ComSlug65Chars_DeveRetornarSlugTamanho()
    {
        string valor65 = "a" + new string('b', 63) + "c"; // 65 chars
        Result<Slug> resultado = Slug.From(valor65);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugTamanho);
    }

    [Theory(DisplayName = "Slug em formato inválido retorna SlugFormatoInvalido")]
    [InlineData("1ceps")]          // começa com dígito
    [InlineData("-ceps")]          // começa com hífen
    [InlineData("CEPS")]           // maiúsculas
    [InlineData("ceps-")]          // termina com hífen
    [InlineData("ceps unifesspa")] // espaço
    [InlineData("ceps--unifesspa")] // hífens consecutivos
    public void From_ComFormatoInvalido_DeveRetornarSlugFormatoInvalido(string valor)
    {
        Result<Slug> resultado = Slug.From(valor);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(UnidadeErrorCodes.SlugFormatoInvalido);
    }

    [Fact(DisplayName = "ToString retorna o Valor")]
    public void ToString_DeveRetornarOValor()
    {
        Slug slug = Slug.From("ceps").Value!;

        slug.ToString().Should().Be("ceps");
    }

    [Fact(DisplayName = "Igualdade estrutural: mesmos valores são iguais")]
    public void Equality_ComMesmosValores_DevemSerIguais()
    {
        Slug a = Slug.From("ceps").Value!;
        Slug b = Slug.From("ceps").Value!;

        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }
}
