namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Errors;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed class NomeDeCampoNoPayloadTests
{
    [Theory(DisplayName = "O caminho vira camelCase por segmento e preserva os índices")]
    [InlineData("Nome", "nome")]
    [InlineData("Quadro[0].Quantidade", "quadro[0].quantidade")]
    [InlineData("[0].Documento.Aplicabilidade.Condicoes[0].Fato", "[0].documento.aplicabilidade.condicoes[0].fato")]
    [InlineData("Matriz[1][2].Valor", "matriz[1][2].valor")]
    public void De_CaminhoComSegmentosEIndices_ConverteSoOsNomes(string caminho, string esperado) =>
        NomeDeCampoNoPayload.De(caminho).Should().Be(esperado);

    [Theory(DisplayName = "Caminho ausente ou vazio aponta a raiz do documento")]
    [InlineData(null)]
    [InlineData("")]
    public void De_CaminhoVazio_DevolveVazio(string? caminho) =>
        NomeDeCampoNoPayload.De(caminho).Should().BeEmpty();
}
