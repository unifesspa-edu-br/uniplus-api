namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CitacaoDaFaseTests
{
    [Fact(DisplayName = "A citação escreve o nome da fase entre aspas simples")]
    public void ToString_EscreveONomeEntreAspasSimples() =>
        new CitacaoDaFase("Inscrição").ToString().Should().Be("'Inscrição'");

    [Theory(DisplayName = "A citação recusa nome ausente, vazio ou em branco")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Construtor_NomeAusenteOuEmBranco_Recusa(string? nome)
    {
        Action criar = () => _ = new CitacaoDaFase(nome!);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A citação com artigo diz 'a fase' e o nome citado")]
    public void ComArtigo_ComFase_DizAFaseEONome() =>
        CitacaoDaFase.ComArtigo(new CitacaoDaFase("Inscrição")).Should().Be("a fase 'Inscrição'");

    [Fact(DisplayName = "A citação com artigo sem fase diz só 'a fase'")]
    public void ComArtigo_SemFase_DizSoAFase() =>
        CitacaoDaFase.ComArtigo(null).Should().Be("a fase");
}
