namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

public sealed class NomeDeArquivoEnviadoTests
{
    [Fact]
    public void Normalizar_SubstitutoIsolado_RecusaNome() =>
        NomeDeArquivoEnviado.Normalizar("arquivo\uD800", "pdf").Should().BeNull();

    [Fact]
    public void Normalizar_ParDeSubstitutos_AceitaEmoji() =>
        NomeDeArquivoEnviado.Normalizar("arquivo😀", "pdf").Should().Be("arquivo😀.pdf");

    [Fact]
    public void Normalizar_SubstitutoIsoladoAntesDaExtensaoPertoDoLimite_RecusaNome() =>
        NomeDeArquivoEnviado.Normalizar(new string('a', 115) + "\uD800.pdf", "pdf").Should().BeNull();
}
