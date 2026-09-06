namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// A chave de ordenação de um keyset multi-coluna precisa sobreviver à ida e
/// volta pelo cursor sem ambiguidade, e recusar o que não é chave — senão a
/// paginação continua de um ponto que ninguém pediu.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class SortKeyCompostaTests
{
    [Fact(DisplayName = "As partes voltam exatamente como entraram")]
    public void IdaEVolta_PreservaAsPartes()
    {
        string chave = SortKeyComposta.Serializar("engenharia civil", "ENG-CIV");

        bool lido = SortKeyComposta.TentarDesserializar(chave, 2, out IReadOnlyList<string> partes);

        lido.Should().BeTrue();
        partes.Should().Equal("engenharia civil", "ENG-CIV");
    }

    [Fact(DisplayName = "Parte que contém o separador não desloca a leitura")]
    public void Parte_ComSeparador_NaoDeslocaLeitura()
    {
        // Um nome de curso pode conter dois-pontos e dígitos; o formato lê por
        // contagem, então o conteúdo nunca é confundido com estrutura.
        string chave = SortKeyComposta.Serializar("3:nao", "12:isto nao e prefixo");

        SortKeyComposta.TentarDesserializar(chave, 2, out IReadOnlyList<string> partes)
            .Should().BeTrue();
        partes.Should().Equal("3:nao", "12:isto nao e prefixo");
    }

    [Fact(DisplayName = "Parte vazia é preservada, e não some da chave")]
    public void ParteVazia_EhPreservada()
    {
        string chave = SortKeyComposta.Serializar(string.Empty, "COD");

        SortKeyComposta.TentarDesserializar(chave, 2, out IReadOnlyList<string> partes)
            .Should().BeTrue();
        partes.Should().Equal(string.Empty, "COD");
    }

    [Theory(DisplayName = "Chave malformada é recusada, em vez de lida pela metade")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("5:abc")]
    [InlineData("3:abc")]
    [InlineData("-1:abc")]
    [InlineData("3:abcextra")]
    [InlineData(":abc")]
    public void ChaveMalformada_EhRecusada(string chave)
    {
        SortKeyComposta.TentarDesserializar(chave, 2, out IReadOnlyList<string> partes)
            .Should().BeFalse();
        partes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Chave de outra ordenação, com mais colunas, é recusada")]
    public void ChaveComQuantidadeDiferente_EhRecusada()
    {
        string chave = SortKeyComposta.Serializar("a", "b", "c");

        SortKeyComposta.TentarDesserializar(chave, 2, out _).Should().BeFalse();
    }

    [Fact(DisplayName = "Chave ausente é recusada")]
    public void ChaveNula_EhRecusada()
    {
        SortKeyComposta.TentarDesserializar(null, 2, out _).Should().BeFalse();
    }
}
