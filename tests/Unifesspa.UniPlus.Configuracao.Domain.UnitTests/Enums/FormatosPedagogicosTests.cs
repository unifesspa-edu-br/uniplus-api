namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do formato pedagógico é por allowlist textual explícita (#749): só
/// os três tokens canônicos UPPER_SNAKE são aceitos. O default PRESENCIAL quando
/// o token está ausente é regra da entidade, não deste mapeamento.
/// </summary>
public sealed class FormatosPedagogicosTests
{
    [Theory(DisplayName = "Os três tokens canônicos são analisados para o formato correto")]
    [InlineData("PRESENCIAL", FormatoPedagogico.Presencial)]
    [InlineData("SEMIPRESENCIAL", FormatoPedagogico.Semipresencial)]
    [InlineData("EAD", FormatoPedagogico.Ead)]
    public void TryAnalisar_TokenCanonico_Resolve(string token, FormatoPedagogico esperado)
    {
        FormatosPedagogicos.TryAnalisar(token, out FormatoPedagogico formato).Should().BeTrue();
        formato.Should().Be(esperado);
    }

    [Fact(DisplayName = "Token com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        FormatosPedagogicos.TryAnalisar("  EAD  ", out FormatoPedagogico formato).Should().BeTrue();
        formato.Should().Be(FormatoPedagogico.Ead);
    }

    [Theory(DisplayName = "Tokens numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]               // numérico — Enum.TryParse aceitaria; a allowlist não
    [InlineData("3")]
    [InlineData("Presencial")]      // PascalCase do enum — não é o token de contrato
    [InlineData("Ead")]
    [InlineData("HIBRIDO")]         // fora do domínio fechado
    [InlineData("presencial")]      // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string token)
    {
        FormatosPedagogicos.TryAnalisar(token, out FormatoPedagogico formato).Should().BeFalse();
        formato.Should().Be(FormatoPedagogico.Nenhum);
        FormatosPedagogicos.EhValido(token).Should().BeFalse();
    }

    [Fact(DisplayName = "Token nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        FormatosPedagogicos.TryAnalisar(null, out FormatoPedagogico formato).Should().BeFalse();
        formato.Should().Be(FormatoPedagogico.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(FormatoPedagogico.Presencial, "PRESENCIAL")]
    [InlineData(FormatoPedagogico.Semipresencial, "SEMIPRESENCIAL")]
    [InlineData(FormatoPedagogico.Ead, "EAD")]
    public void ParaTokenCanonico_RoundTrip(FormatoPedagogico formato, string token)
    {
        FormatosPedagogicos.ParaTokenCanonico(formato).Should().Be(token);
        FormatosPedagogicos.TryAnalisar(token, out FormatoPedagogico resolvido).Should().BeTrue();
        resolvido.Should().Be(formato);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (sentinela) lança — não é formato válido")]
    public void ParaTokenCanonico_Nenhum_Lanca()
    {
        Action act = () => FormatosPedagogicos.ParaTokenCanonico(FormatoPedagogico.Nenhum);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "TokensCanonicos lista exatamente os três valores de domínio")]
    public void TokensCanonicos_TemTresValores()
    {
        FormatosPedagogicos.TokensCanonicos.Should().HaveCount(3)
            .And.Contain(["PRESENCIAL", "SEMIPRESENCIAL", "EAD"]);
    }
}
