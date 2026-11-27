namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Idempotency;

using System.Text;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

public sealed class RequestBodyReaderTests
{
    [Fact(DisplayName = "O corpo lido volta ao início para o model binder consumi-lo")]
    public async Task CorpoLido_VoltaAoInicio()
    {
        DefaultHttpContext http = new();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"a\":1}"));

        byte[] lido = await RequestBodyReader.ReadWithoutConsumingAsync(http.Request, CancellationToken.None);

        Encoding.UTF8.GetString(lido).Should().Be("{\"a\":1}");
        http.Request.Body.Position.Should().Be(0);
    }

    [Fact(DisplayName = "O corpo com comprimento declarado zero é lido como vazio")]
    public async Task ComprimentoZero_LeVazio()
    {
        DefaultHttpContext http = new();
        http.Request.ContentLength = 0;

        byte[] lido = await RequestBodyReader.ReadWithoutConsumingAsync(http.Request, CancellationToken.None);

        lido.Should().BeEmpty();
    }

    [Fact(DisplayName = "A exceção 413 que o servidor lança ao ler além do limite sobe sem ser traduzida")]
    public async Task LeituraAlemDoLimite_PropagaAExcecao()
    {
        DefaultHttpContext http = new();
        http.Request.Body = new CorpoQueEstouraOLimite();

        Func<Task> ler = () => RequestBodyReader.ReadWithoutConsumingAsync(http.Request, CancellationToken.None);

        (await ler.Should().ThrowAsync<BadHttpRequestException>()).Which.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    private sealed class CorpoQueEstouraOLimite : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
    }
}
