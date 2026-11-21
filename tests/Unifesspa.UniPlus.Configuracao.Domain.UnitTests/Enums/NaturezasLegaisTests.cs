namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// A natureza da modalidade responde se ela é cota da Lei 12.711, ação afirmativa
/// institucional ou ampla concorrência (UNI-REQ-0141). O parsing é por lista textual de códigos aceitos:
/// só os três códigos canônicos UPPER_SNAKE são aceitos.
/// </summary>
public sealed class NaturezasLegaisTests
{
    [Theory(DisplayName = "Os três códigos canônicos são analisados para a natureza correta")]
    [InlineData("COTA_RESERVADA", NaturezaLegal.CotaReservada)]
    [InlineData("AMPLA", NaturezaLegal.Ampla)]
    [InlineData("ACAO_AFIRMATIVA", NaturezaLegal.AcaoAfirmativa)]
    public void TryAnalisar_CodigoCanonico_Resolve(string codigo, NaturezaLegal esperada)
    {
        NaturezasLegais.TryAnalisar(codigo, out NaturezaLegal natureza).Should().BeTrue();
        natureza.Should().Be(esperada);
    }

    [Theory(DisplayName = "SUPLEMENTAR e OUTRA_MODALIDADE deixaram de ser natureza e são rejeitados")]
    [InlineData("SUPLEMENTAR")]
    [InlineData("OUTRA_MODALIDADE")]
    public void TryAnalisar_CodigoRetirado_Rejeita(string codigo)
    {
        NaturezasLegais.TryAnalisar(codigo, out NaturezaLegal natureza).Should().BeFalse(
            "a ação afirmativa absorveu as duas naturezas; aceitá-las devolveria ao cadastro "
            + "modalidade que não diz se é cota, ação afirmativa ou ampla concorrência");
        natureza.Should().Be(NaturezaLegal.Nenhuma);
        NaturezasLegais.EhValido(codigo).Should().BeFalse();
    }

    [Theory(DisplayName = "Códigos numéricos, PascalCase, minúsculos e vazios são rejeitados")]
    [InlineData("3")]               // numérico — Enum.TryParse aceitaria; a lista de códigos aceitos não
    [InlineData("AcaoAfirmativa")]  // PascalCase do enum — não é o código de contrato
    [InlineData("acao_afirmativa")] // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string codigo)
    {
        NaturezasLegais.TryAnalisar(codigo, out NaturezaLegal natureza).Should().BeFalse();
        natureza.Should().Be(NaturezaLegal.Nenhuma);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(NaturezaLegal.CotaReservada, "COTA_RESERVADA")]
    [InlineData(NaturezaLegal.Ampla, "AMPLA")]
    [InlineData(NaturezaLegal.AcaoAfirmativa, "ACAO_AFIRMATIVA")]
    public void ParaCodigoCanonico_IdaEVolta(NaturezaLegal natureza, string codigo)
    {
        NaturezasLegais.ParaTokenCanonico(natureza).Should().Be(codigo);
        NaturezasLegais.TryAnalisar(codigo, out NaturezaLegal resolvida).Should().BeTrue();
        resolvida.Should().Be(natureza);
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente as três naturezas")]
    public void TokensCanonicos_TemTresValores() =>
        NaturezasLegais.TokensCanonicos.Should().BeEquivalentTo(["COTA_RESERVADA", "AMPLA", "ACAO_AFIRMATIVA"]);
}
