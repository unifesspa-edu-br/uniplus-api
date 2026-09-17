namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Formatting;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Formatting;

/// <summary>
/// Cobertura da comparação de selo em requisições condicionais — em especial o caso que faz a
/// revalidação deixar de funcionar em silêncio: o selo enfraquecido por um intermediário.
/// </summary>
public sealed class SeloDeEntidadeTests
{
    private const string SeloAtual = "\"1:abc123\"";

    [Fact(DisplayName = "Selo idêntico coincide")]
    public void IfNoneMatchCoincide_SeloIdentico_Coincide() =>
        SeloDeEntidade.IfNoneMatchCoincide(SeloAtual, SeloAtual).Should().BeTrue();

    [Fact(DisplayName = "Selo enfraquecido pelo intermediário coincide com o selo forte de origem")]
    public void IfNoneMatchCoincide_SeloEnfraquecido_Coincide()
    {
        // nginx com gzip reescreve ETag: "x" como W/"x". O cliente devolve o selo enfraquecido; uma
        // comparação byte a byte não o reconheceria e responderia o corpo inteiro toda vez.
        SeloDeEntidade.IfNoneMatchCoincide("W/" + SeloAtual, SeloAtual).Should().BeTrue();
    }

    [Fact(DisplayName = "Selo de outra representação não coincide, mesmo enfraquecido")]
    public void IfNoneMatchCoincide_OutroSelo_NaoCoincide()
    {
        SeloDeEntidade.IfNoneMatchCoincide("\"1:outro\"", SeloAtual).Should().BeFalse();
        SeloDeEntidade.IfNoneMatchCoincide("W/\"1:outro\"", SeloAtual).Should().BeFalse();
    }

    [Fact(DisplayName = "Lista separada por vírgula coincide quando um dos selos é o corrente")]
    public void IfNoneMatchCoincide_Lista_CoincideNoSegundo() =>
        SeloDeEntidade.IfNoneMatchCoincide("\"1:antigo\", W/\"1:abc123\"", SeloAtual).Should().BeTrue();

    [Fact(DisplayName = "Curinga coincide com a representação existente")]
    public void IfNoneMatchCoincide_Curinga_Coincide() =>
        SeloDeEntidade.IfNoneMatchCoincide("*", SeloAtual).Should().BeTrue();

    [Theory(DisplayName = "Cabeçalho ausente ou vazio nunca coincide")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IfNoneMatchCoincide_Ausente_NaoCoincide(string? ifNoneMatch) =>
        SeloDeEntidade.IfNoneMatchCoincide(ifNoneMatch, SeloAtual).Should().BeFalse();

    [Fact(DisplayName = "A marca de fraqueza é sensível a caixa — 'w/' não é marca, é selo malformado")]
    public void IfNoneMatchCoincide_MarcaEmCaixaBaixa_NaoCoincide() =>
        SeloDeEntidade.IfNoneMatchCoincide("w/" + SeloAtual, SeloAtual).Should().BeFalse();
}
