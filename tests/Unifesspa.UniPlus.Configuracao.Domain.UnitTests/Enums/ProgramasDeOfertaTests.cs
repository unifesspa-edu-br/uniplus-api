namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do programa de oferta é por allowlist textual explícita (#749): só
/// os sete tokens canônicos UPPER_SNAKE são aceitos. Tokens numéricos e nomes
/// PascalCase do enum são rejeitados — o que <c>Enum.TryParse</c> aceitaria por
/// engano.
/// </summary>
public sealed class ProgramasDeOfertaTests
{
    [Theory(DisplayName = "Os sete tokens canônicos são analisados para o programa correto")]
    [InlineData("REGULAR", ProgramaDeOferta.Regular)]
    [InlineData("FORMA_PARA", ProgramaDeOferta.FormaPara)]
    [InlineData("PARFOR", ProgramaDeOferta.Parfor)]
    [InlineData("PRONERA", ProgramaDeOferta.Pronera)]
    [InlineData("PEPETI", ProgramaDeOferta.Pepeti)]
    [InlineData("CONVENIO_OUTRO", ProgramaDeOferta.ConvenioOutro)]
    [InlineData("OUTRO", ProgramaDeOferta.Outro)]
    public void TryAnalisar_TokenCanonico_Resolve(string token, ProgramaDeOferta esperado)
    {
        ProgramasDeOferta.TryAnalisar(token, out ProgramaDeOferta programa).Should().BeTrue();
        programa.Should().Be(esperado);
    }

    [Fact(DisplayName = "Token com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        ProgramasDeOferta.TryAnalisar("  PARFOR  ", out ProgramaDeOferta programa).Should().BeTrue();
        programa.Should().Be(ProgramaDeOferta.Parfor);
    }

    [Theory(DisplayName = "Tokens numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]           // numérico — Enum.TryParse aceitaria; a allowlist não
    [InlineData("7")]
    [InlineData("Regular")]     // PascalCase do enum — não é o token de contrato
    [InlineData("Parfor")]
    [InlineData("PROUNI")]      // fora do domínio fechado
    [InlineData("regular")]     // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string token)
    {
        ProgramasDeOferta.TryAnalisar(token, out ProgramaDeOferta programa).Should().BeFalse();
        programa.Should().Be(ProgramaDeOferta.Nenhum);
        ProgramasDeOferta.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "Token nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        ProgramasDeOferta.TryAnalisar(null, out ProgramaDeOferta programa).Should().BeFalse();
        programa.Should().Be(ProgramaDeOferta.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(ProgramaDeOferta.Regular, "REGULAR")]
    [InlineData(ProgramaDeOferta.FormaPara, "FORMA_PARA")]
    [InlineData(ProgramaDeOferta.ConvenioOutro, "CONVENIO_OUTRO")]
    public void ParaTokenCanonico_RoundTrip(ProgramaDeOferta programa, string token)
    {
        ProgramasDeOferta.ParaTokenCanonico(programa).Should().Be(token);
        ProgramasDeOferta.TryAnalisar(token, out ProgramaDeOferta resolvido).Should().BeTrue();
        resolvido.Should().Be(programa);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (sentinela) lança — não é programa válido")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => ProgramasDeOferta.ParaTokenCanonico(ProgramaDeOferta.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os sete valores de domínio")]
    public void TokensCanonicos_TemSeteValores()
    {
        ProgramasDeOferta.TokensCanonicos.Should().HaveCount(7)
            .And.Contain(["REGULAR", "FORMA_PARA", "PARFOR", "PRONERA", "PEPETI", "CONVENIO_OUTRO", "OUTRO"]);
    }
}
