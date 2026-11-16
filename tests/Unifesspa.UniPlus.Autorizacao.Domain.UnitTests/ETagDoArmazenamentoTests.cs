namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class ETagDoArmazenamentoTests
{
    [Theory(DisplayName = "O ETag ausente, em branco ou com caractere nulo não vale")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"etag\0\"")]
    public void Normalizar_Invalido_DevolveNulo(string? etag) =>
        ETagDoArmazenamento.Normalizar(etag).Should().BeNull();

    [Fact(DisplayName = "O ETag sem espaços nas pontas vale")]
    public void Normalizar_ComEspacos_Apara() =>
        ETagDoArmazenamento.Normalizar("  \"etag\"  ").Should().Be("\"etag\"");

    [Fact(DisplayName = "O ETag mede o tamanho antes de aparar: o limite vale, e um caractere a mais não")]
    public void Normalizar_Limite_MedeOTextoCru()
    {
        ETagDoArmazenamento.Normalizar(new string('a', ETagDoArmazenamento.MaxLength)).Should().NotBeNull();
        ETagDoArmazenamento.Normalizar(new string('a', ETagDoArmazenamento.MaxLength) + " ").Should().BeNull();
    }
}
