namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Text;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Http;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class CorpoDoDocumentoTests
{
    private static MemoryStream Corpo(int bytes) => new(Enumerable.Repeat((byte)'a', bytes).ToArray());

    [Fact(DisplayName = "O corpo no teto exato é lido por inteiro")]
    public async Task CorpoNoTeto_ELido()
    {
        Result<string> resultado = await CorpoDoDocumento.LerAsync(Corpo(LimitesDoDocumento.TamanhoMaximoEmBytes), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Length.Should().Be(LimitesDoDocumento.TamanhoMaximoEmBytes);
    }

    [Fact(DisplayName = "A exceção 413 que o servidor lança ao ler além do limite sobe sem ser traduzida")]
    public async Task LeituraAlemDoLimite_PropagaAExcecaoDoServidor()
    {
        using CorpoQueEstouraOLimite corpo = new();

        Func<Task> leitura = () => CorpoDoDocumento.LerAsync(corpo, CancellationToken.None);

        (await leitura.Should().ThrowAsync<BadHttpRequestException>()).Which.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact(DisplayName = "O corpo vazio é texto vazio, e a forma o recusa depois")]
    public async Task CorpoVazio_TextoVazio()
    {
        Result<string> resultado = await CorpoDoDocumento.LerAsync(new MemoryStream(), CancellationToken.None);

        resultado.Value.Should().BeEmpty();
    }

    [Fact(DisplayName = "O acento em UTF-8 é lido sem troca de caracteres")]
    public async Task Utf8_ELidoSemTroca()
    {
        using MemoryStream corpo = new(Encoding.UTF8.GetBytes("Seleção — Marabá"));

        Result<string> resultado = await CorpoDoDocumento.LerAsync(corpo, CancellationToken.None);

        resultado.Value.Should().Be("Seleção — Marabá");
    }

    [Fact(DisplayName = "O byte que não é UTF-8 válido recusa como forma inválida, sem trocar o caractere")]
    public async Task Utf8Invalido_FormaInvalida()
    {
        using MemoryStream corpo = new([0x7B, 0xFF, 0xFE, 0x7D]);

        Result<string> resultado = await CorpoDoDocumento.LerAsync(corpo, CancellationToken.None);

        resultado.Error!.Code.Should().Be(DocumentoDaConfiguracaoErrorCodes.FormaInvalida);
    }

    [Fact(DisplayName = "O fluxo nulo é recusado")]
    public async Task FluxoNulo_Lanca()
    {
        Func<Task> leitura = () => CorpoDoDocumento.LerAsync(null!, CancellationToken.None);

        await leitura.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class CorpoQueEstouraOLimite : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
    }
}
