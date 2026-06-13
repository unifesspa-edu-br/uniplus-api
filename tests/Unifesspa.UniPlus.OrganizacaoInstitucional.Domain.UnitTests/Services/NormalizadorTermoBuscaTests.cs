namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Services;

public sealed class NormalizadorTermoBuscaTests
{
    [Theory(DisplayName = "Normalizar devolve string vazia para entrada nula ou em branco")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Normalizar_EntradaVazia_RetornaVazio(string? entrada)
    {
        NormalizadorTermoBusca.Normalizar(entrada).Should().BeEmpty();
    }

    [Theory(DisplayName = "Normalizar remove diacríticos e dobra a caixa para maiúsculas")]
    [InlineData("CEPS", "CEPS")]
    [InlineData("ceps", "CEPS")]
    [InlineData("Educação", "EDUCACAO")]
    [InlineData("José", "JOSE")]
    [InlineData("Núcleo", "NUCLEO")]
    [InlineData("Açaí", "ACAI")]
    [InlineData("Pró-Reitoria", "PRO-REITORIA")]
    [InlineData("Ã É Í Õ Ç Ñ Ü", "A E I O C N U")]
    public void Normalizar_RemoveAcentosEDobraCaixa(string entrada, string esperado)
    {
        NormalizadorTermoBusca.Normalizar(entrada).Should().Be(esperado);
    }

    [Fact(DisplayName = "Normalizar apara espaços nas pontas")]
    public void Normalizar_AparaEspacos()
    {
        NormalizadorTermoBusca.Normalizar("  centro  ").Should().Be("CENTRO");
    }

    [Fact(DisplayName = "ParaIndice concatena os campos pesquisáveis normalizados")]
    public void ParaIndice_ConcatenaCamposNormalizados()
    {
        string indice = NormalizadorTermoBusca.ParaIndice(
            nome: "Faculdade de Educação",
            sigla: "FACED",
            codigo: "0007",
            slug: "faced",
            alias: "Educação Física");

        indice.Should().Be("FACULDADE DE EDUCACAO FACED 0007 FACED EDUCACAO FISICA");
    }

    [Fact(DisplayName = "ParaIndice trata alias nulo sem espaço sobrando")]
    public void ParaIndice_AliasNulo_NaoDeixaEspacoSobrando()
    {
        string indice = NormalizadorTermoBusca.ParaIndice(
            nome: "Centro",
            sigla: "CEPS",
            codigo: "0001",
            slug: "ceps",
            alias: null);

        indice.Should().Be("CENTRO CEPS 0001 CEPS");
    }
}
