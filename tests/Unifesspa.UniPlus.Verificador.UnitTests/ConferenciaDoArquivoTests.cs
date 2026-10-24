namespace Unifesspa.UniPlus.Verificador.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Verificador.Contratos;
using Unifesspa.UniPlus.Verificador.Verificacao;

public sealed class ConferenciaDoArquivoTests
{
    [Theory(DisplayName = "Só o PDF que exige senha para abrir é detectado; o protegido só de proprietário abre")]
    [InlineData("senha-de-abertura.pdf", true)]
    [InlineData("so-de-proprietario.pdf", false)]
    [InlineData("sem-senha.pdf", false)]
    public void PdfExigeSenhaParaAbrir_SegueAAberturaComSenhaVazia(string fixture, bool esperado)
    {
        using MemoryStream pdf = new(Cenario.Fixture(fixture));

        ConferenciaDoArquivo.PdfExigeSenhaParaAbrir(pdf).Should().Be(esperado);
    }

    [Fact(DisplayName = "PDF que faz a PdfPig falhar fora do tratamento de senha não conta como exigir senha")]
    public void PdfQueQuebraAPdfPig_NaoExigeSenha()
    {
        using MemoryStream pdf = new(Cenario.Fixture("corrompido-apos-o-cabecalho.pdf"));

        ConferenciaDoArquivo.PdfExigeSenhaParaAbrir(pdf).Should().BeFalse();
    }

    [Theory(DisplayName = "O formato é conferido pelos primeiros bytes")]
    [InlineData(FormatoDoArquivo.Pdf, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, true)]
    [InlineData(FormatoDoArquivo.Jpeg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData(FormatoDoArquivo.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, true)]
    [InlineData(FormatoDoArquivo.Png, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, false)]
    [InlineData(FormatoDoArquivo.NaoDefinido, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, false)]
    public void FormatoCorresponde_ConfereAAssinatura(FormatoDoArquivo formato, byte[] inicio, bool esperado) =>
        ConferenciaDoArquivo.FormatoCorresponde(formato, inicio).Should().Be(esperado);
}
