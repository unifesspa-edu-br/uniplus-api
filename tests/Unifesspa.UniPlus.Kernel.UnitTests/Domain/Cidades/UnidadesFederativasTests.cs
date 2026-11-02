namespace Unifesspa.UniPlus.Kernel.UnitTests.Domain.Cidades;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;

/// <summary>A lista fixa das 27 UFs responde o nome pela sigla e pelo código IBGE.</summary>
public sealed class UnidadesFederativasTests
{
    [Fact(DisplayName = "A lista tem as 27 UFs, sem sigla nem código repetidos")]
    public void Todas_TemAs27SemRepeticao()
    {
        UnidadesFederativas.Todas.Should().HaveCount(27);
        UnidadesFederativas.Todas.Select(static uf => uf.Sigla).Should().OnlyHaveUniqueItems();
        UnidadesFederativas.Todas.Select(static uf => uf.CodigoIbge).Should().OnlyHaveUniqueItems();
    }

    [Theory(DisplayName = "A sigla e o código IBGE da mesma UF dão o mesmo nome")]
    [InlineData("PA", "15", "Pará")]
    [InlineData("DF", "53", "Distrito Federal")]
    [InlineData("SP", "35", "São Paulo")]
    public void SiglaECodigo_DaoOMesmoNome(string sigla, string codigo, string nome)
    {
        UnidadesFederativas.PorSigla(sigla)!.Nome.Should().Be(nome);
        UnidadesFederativas.PorCodigoIbge(codigo)!.Nome.Should().Be(nome);
        UnidadesFederativas.PorSigla(sigla).Should().BeSameAs(UnidadesFederativas.PorCodigoIbge(codigo));
    }

    [Theory(DisplayName = "Valor que não é UF não tem nome")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("pa")]
    public void ValorInvalido_SemUf(string? sigla) => UnidadesFederativas.PorSigla(sigla).Should().BeNull();
}
