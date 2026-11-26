namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

using AwesomeAssertions;

/// <summary>A leitura de um documento gravado inválido falha na hora, com mensagem fixa e sem a exceção da biblioteca como causa.</summary>
internal static class RecusaDeDocumentoGravado
{
    private const string OrientacaoDaMensagem = "Verifique se houve alteração manual da coluna fora do fluxo da aplicação.";

    public static void DeveRecusarComoDocumentoInvalido(this Action leitura, string nomeDoValor)
    {
        InvalidOperationException erro = leitura.Should().Throw<InvalidOperationException>().Which;
        ConferirMensagemFixa(erro, nomeDoValor);
    }

    /// <summary>Além da mensagem fixa, o conteúdo gravado — <paramref name="marcador"/> — não aparece na mensagem.</summary>
    public static void DeveRecusarComoDocumentoInvalido(this Action leitura, string nomeDoValor, string marcador)
    {
        InvalidOperationException erro = leitura.Should().Throw<InvalidOperationException>().Which;
        ConferirMensagemFixa(erro, nomeDoValor);
        erro.Message.Should().NotContain(marcador);
    }

    /// <summary>A leitura assíncrona recusa como <see cref="DeveRecusarComoDocumentoInvalido(Action, string, string)"/>.</summary>
    public static async Task DeveRecusarComoDocumentoInvalidoAsync(this Func<Task> leitura, string nomeDoValor, string marcador)
    {
        InvalidOperationException erro = (await leitura.Should().ThrowAsync<InvalidOperationException>()).Which;
        ConferirMensagemFixa(erro, nomeDoValor);
        erro.Message.Should().NotContain(marcador);
    }

    private static void ConferirMensagemFixa(InvalidOperationException erro, string nomeDoValor)
    {
        erro.Message.Should().Be(
            $"Dado inválido no banco ao reidratar {nomeDoValor}: documento JSON inválido. {OrientacaoDaMensagem}");
        erro.InnerException.Should().BeNull();
    }
}
