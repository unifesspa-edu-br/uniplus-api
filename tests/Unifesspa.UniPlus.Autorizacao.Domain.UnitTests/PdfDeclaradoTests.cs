namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

public sealed class PdfDeclaradoTests
{
    [Theory(DisplayName = "O tamanho declarado vale de 1 byte ao máximo")]
    [InlineData(1L, true)]
    [InlineData(PdfDeclarado.TamanhoMaximoBytes, true)]
    [InlineData(0L, false)]
    [InlineData(-1L, false)]
    [InlineData(PdfDeclarado.TamanhoMaximoBytes + 1, false)]
    public void TamanhoAdmitido_ConfereOsLimites(long tamanho, bool esperado) =>
        PdfDeclarado.TamanhoAdmitido(tamanho).Should().Be(esperado);

    [Fact(DisplayName = "O máximo em bytes é o máximo em megabytes")]
    public void TamanhoMaximo_BytesEMegabytesConcordam() =>
        PdfDeclarado.TamanhoMaximoBytes.Should().Be(PdfDeclarado.TamanhoMaximoEmMegabytes * 1024L * 1024L);
}
