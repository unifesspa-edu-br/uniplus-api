namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// O parsing do formato pedagógico é por lista textual explícita de códigos aceitos (#749): só
/// os três códigos canônicos UPPER_SNAKE são aceitos. O default PRESENCIAL quando
/// o código está ausente é regra da entidade, não deste mapeamento.
/// </summary>
public sealed class FormatosPedagogicosTests
{
    [Theory(DisplayName = "Os três códigos canônicos são analisados para o formato correto")]
    [InlineData("PRESENCIAL", FormatoPedagogico.Presencial)]
    [InlineData("SEMIPRESENCIAL", FormatoPedagogico.Semipresencial)]
    [InlineData("EAD", FormatoPedagogico.Ead)]
    public void TryAnalisar_CodigoCanonico_Resolve(string codigo, FormatoPedagogico esperado)
    {
        FormatosPedagogicos.TryAnalisar(codigo, out FormatoPedagogico formato).Should().BeTrue();
        formato.Should().Be(esperado);
    }

    [Fact(DisplayName = "Código com espaços é normalizado por Trim antes da resolução")]
    public void TryAnalisar_ComEspacos_Normaliza()
    {
        FormatosPedagogicos.TryAnalisar("  EAD  ", out FormatoPedagogico formato).Should().BeTrue();
        formato.Should().Be(FormatoPedagogico.Ead);
    }

    [Theory(DisplayName = "Códigos numéricos, PascalCase, fora do domínio e vazios são rejeitados")]
    [InlineData("1")]               // numérico — Enum.TryParse aceitaria; a lista de códigos aceitos não
    [InlineData("3")]
    [InlineData("Presencial")]      // PascalCase do enum — não é o código de contrato
    [InlineData("Ead")]
    [InlineData("HIBRIDO")]         // fora do domínio fechado
    [InlineData("presencial")]      // case-sensitive
    [InlineData("")]
    [InlineData("   ")]
    public void TryAnalisar_ForaDoDominio_Rejeita(string codigo)
    {
        FormatosPedagogicos.TryAnalisar(codigo, out FormatoPedagogico formato).Should().BeFalse();
        formato.Should().Be(FormatoPedagogico.Nenhum);
        FormatosPedagogicos.EhValido(codigo).Should().BeFalse();
    }

    [Fact(DisplayName = "Código nulo é rejeitado sem lançar")]
    public void TryAnalisar_Nulo_Rejeita()
    {
        FormatosPedagogicos.TryAnalisar(null, out FormatoPedagogico formato).Should().BeFalse();
        formato.Should().Be(FormatoPedagogico.Nenhum);
    }

    [Theory(DisplayName = "ParaTokenCanonico é o inverso de TryAnalisar (round-trip)")]
    [InlineData(FormatoPedagogico.Presencial, "PRESENCIAL")]
    [InlineData(FormatoPedagogico.Semipresencial, "SEMIPRESENCIAL")]
    [InlineData(FormatoPedagogico.Ead, "EAD")]
    public void ParaCodigoCanonico_IdaEVolta(FormatoPedagogico formato, string codigo)
    {
        FormatosPedagogicos.ParaTokenCanonico(formato).Should().Be(codigo);
        FormatosPedagogicos.TryAnalisar(codigo, out FormatoPedagogico resolvido).Should().BeTrue();
        resolvido.Should().Be(formato);
    }

    [Fact(DisplayName = "ParaTokenCanonico de Nenhum (valor de ausência) lança — não é formato válido")]
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
