namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;

/// <summary>
/// A leitura do arquivo enviado na confirmação: a chave de envio segue sobrescrevível até a URL
/// expirar, então o objeto pode crescer entre o stat e a leitura — o teto é reconferido sobre o
/// que foi de fato lido.
/// </summary>
public sealed class LeituraDoArquivoEnviadoTests
{
    [Fact(DisplayName = "O objeto que cresceu depois do stat é recusado como excedido pelo que foi lido")]
    public async Task LerAsync_ObjetoCresceuDepoisDoStat_Excedido()
    {
        const long Teto = 10;
        IArquivoArmazenadoStorage storage = Substitute.For<IArquivoArmazenadoStorage>();
        storage.ObterInfoAsync("chave", Arg.Any<CancellationToken>()).Returns(new InfoObjetoArmazenado(5, "application/pdf"));
        storage.AbrirLeituraAsync("chave", Teto + 1, Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(new byte[Teto + 1]));

        ArquivoEnviadoLido lido = await LeituraDoArquivoEnviado.LerAsync(storage, "chave", Teto, CancellationToken.None);

        lido.Situacao.Should().Be(SituacaoDoArquivoEnviado.Excedido);
    }
}
