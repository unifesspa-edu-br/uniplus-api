namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;

public sealed class EnvioDoPdfTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "A URL de envio assina a chave como application/pdf por 15 minutos e vale até 15 minutos depois do instante informado")]
    public async Task Emitir_AssinaPdfPorQuinzeMinutos()
    {
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento = Substitute.For<IArmazenamentoDosDocumentosDaAutorizacao>();
        armazenamento.GerarUrlDeEnvioAsync("autorizacao/chave", "application/pdf", TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns("https://armazenamento.test/envio?assinatura=1");

        UrlDeEnvioDoPdf url = await new EnvioDoPdf(armazenamento).EmitirAsync("autorizacao/chave", Agora, CancellationToken.None);

        url.Endereco.AbsoluteUri.Should().Be("https://armazenamento.test/envio?assinatura=1");
        url.ExpiraEm.Should().Be(Agora.AddMinutes(15));
    }

    [Fact(DisplayName = "O prazo em segundos é o do envio, para a chave de idempotência dos endpoints que devolvem a URL")]
    public void Prazo_EmSegundosConcordaComOPrazo() =>
        EnvioDoPdf.PrazoEmSegundos.Should().Be((int)EnvioDoPdf.Prazo.TotalSeconds);
}
