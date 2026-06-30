namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing da categoria é por allowlist textual explícita (#591): só os sete
/// tokens canônicos UPPER_SNAKE são aceitos. Tokens numéricos e nomes PascalCase
/// do enum são rejeitados — o que <c>Enum.TryParse</c> aceitaria por engano.
/// </summary>
public sealed class CategoriaDocumentosTests
{
    [Theory(DisplayName = "Os sete tokens canônicos são analisados para a categoria correta")]
    [InlineData("IDENTIFICACAO", CategoriaDocumento.Identificacao)]
    [InlineData("ESCOLARIDADE", CategoriaDocumento.Escolaridade)]
    [InlineData("RENDA", CategoriaDocumento.Renda)]
    [InlineData("RACA_ETNIA", CategoriaDocumento.RacaEtnia)]
    [InlineData("SAUDE", CategoriaDocumento.Saude)]
    [InlineData("RESIDENCIA", CategoriaDocumento.Residencia)]
    [InlineData("OUTROS", CategoriaDocumento.Outros)]
    public void TryAnalisar_TokenCanonico_Resolve(string token, CategoriaDocumento esperada)
    {
        CategoriaDocumentos.TryAnalisar(token, out CategoriaDocumento categoria).Should().BeTrue();
        categoria.Should().Be(esperada);
    }

    [Fact(DisplayName = "Token com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        CategoriaDocumentos.TryAnalisar("  SAUDE  ", out CategoriaDocumento categoria).Should().BeTrue();
        categoria.Should().Be(CategoriaDocumento.Saude);
    }

    [Theory(DisplayName = "Tokens numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]               // numérico — Enum.TryParse aceitaria; a allowlist não
    [InlineData("4")]
    [InlineData("Identificacao")]   // PascalCase do enum — não é o token de contrato
    [InlineData("Saude")]
    [InlineData("FINANCEIRO")]      // fora do domínio fechado
    [InlineData("identificacao")]   // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string token)
    {
        CategoriaDocumentos.TryAnalisar(token, out CategoriaDocumento categoria).Should().BeFalse();
        categoria.Should().Be(CategoriaDocumento.Nenhum);
        CategoriaDocumentos.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "Token nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        CategoriaDocumentos.TryAnalisar(null, out CategoriaDocumento categoria).Should().BeFalse();
        categoria.Should().Be(CategoriaDocumento.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(CategoriaDocumento.Identificacao, "IDENTIFICACAO")]
    [InlineData(CategoriaDocumento.RacaEtnia, "RACA_ETNIA")]
    [InlineData(CategoriaDocumento.Outros, "OUTROS")]
    public void ParaTokenCanonico_RoundTrip(CategoriaDocumento categoria, string token)
    {
        CategoriaDocumentos.ParaTokenCanonico(categoria).Should().Be(token);
        CategoriaDocumentos.TryAnalisar(token, out CategoriaDocumento resolvida).Should().BeTrue();
        resolvida.Should().Be(categoria);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (sentinela) lança — não é categoria válida")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => CategoriaDocumentos.ParaTokenCanonico(CategoriaDocumento.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os sete valores de domínio")]
    public void TokensCanonicos_TemSeteValores()
    {
        CategoriaDocumentos.TokensCanonicos.Should().HaveCount(7)
            .And.Contain(["IDENTIFICACAO", "ESCOLARIDADE", "RENDA", "RACA_ETNIA", "SAUDE", "RESIDENCIA", "OUTROS"]);
    }
}
