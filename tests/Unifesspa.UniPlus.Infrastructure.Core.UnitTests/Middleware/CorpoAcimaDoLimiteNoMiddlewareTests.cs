namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Middleware;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

using Unifesspa.UniPlus.Infrastructure.Core.Middleware;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.CorpoDaRequisicao;

/// <summary>
/// A leitura que passa do limite do corpo chega como exceção do servidor com status 413, e vira a recusa do limite, com o
/// limite que o servidor aplicou. As demais falhas de leitura seguem como erro interno.
/// </summary>
public sealed class CorpoAcimaDoLimiteNoMiddlewareTests
{
    [Fact(DisplayName = "A leitura além do limite responde 413 com o código do limite e o limite que o servidor aplicou")]
    public async Task InvokeAsync_LeituraAlemDoLimite_Responde413ComOLimiteDoServidor()
    {
        DefaultHttpContext context = ContextoComLimiteDoServidor(2048);
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw LeituraAlemDoLimite());

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        using JsonDocument doc = await MiddlewareDeErrosDeTeste.LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.requisicao.corpo_acima_do_limite");
        doc.RootElement.GetProperty("detail").GetString().Should().Be(
            "O corpo da requisição passa do limite de 2048 bytes aceito por esta operação. Reduza o conteúdo enviado.");
    }

    [Fact(DisplayName = "A resposta não repete a mensagem da exceção do servidor")]
    public async Task InvokeAsync_LeituraAlemDoLimite_NaoRepeteAMensagemDaExcecao()
    {
        DefaultHttpContext context = ContextoComLimiteDoServidor(2048);
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw LeituraAlemDoLimite());

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await MiddlewareDeErrosDeTeste.LerBodyAsync(context);
        doc.RootElement.GetRawText().Should().NotContain("trecho do corpo");
    }

    [Fact(DisplayName = "O registro da leitura em partes além do limite traz só o limite e o caminho, sem a exceção nem o trecho do corpo")]
    public async Task InvokeAsync_LeituraEmPartesAlemDoLimite_RegistraSemAExcecaoNemOTrecho()
    {
        MiddlewareDoLimiteDeTeste.RegistroEmMemoria<GlobalExceptionMiddleware> registro = new();
        DefaultHttpContext context = ContextoComLimiteDoServidor(2048);
        context.Request.Path = "/teste/rota";
        context.Request.ContentLength = null;
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw LeituraAlemDoLimite(), registro);

        await middleware.InvokeAsync(context);

        registro.Entradas.Should().ContainSingle();
        registro.Entradas.Should().AllSatisfy(e => e.Mensagem.Should().NotContain("trecho do corpo"));
        registro.Excecoes.Should().BeEmpty();
    }

    [Fact(DisplayName = "Sem o servidor informar o limite, a resposta usa o limite padrão de 2 MiB")]
    public async Task InvokeAsync_SemLimiteDoServidor_UsaOPadrao()
    {
        DefaultHttpContext context = MiddlewareDeErrosDeTeste.CriarContexto();
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(_ => throw LeituraAlemDoLimite());

        await middleware.InvokeAsync(context);

        using JsonDocument doc = await MiddlewareDeErrosDeTeste.LerBodyAsync(context);
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("limite de 2097152 bytes");
    }

    [Fact(DisplayName = "A falha de leitura que não é de tamanho continua como erro interno")]
    public async Task InvokeAsync_FalhaDeLeituraSemSerDeTamanho_SegueComoErroInterno()
    {
        DefaultHttpContext context = MiddlewareDeErrosDeTeste.CriarContexto();
        GlobalExceptionMiddleware middleware = MiddlewareDeErrosDeTeste.Criar(
            _ => throw new BadHttpRequestException("corpo ilegível", StatusCodes.Status400BadRequest));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        using JsonDocument doc = await MiddlewareDeErrosDeTeste.LerBodyAsync(context);
        doc.RootElement.GetProperty("code").GetString().Should().Be("uniplus.internal.unexpected");
    }

    private static BadHttpRequestException LeituraAlemDoLimite() =>
        new("trecho do corpo recebido", StatusCodes.Status413PayloadTooLarge);

    private static DefaultHttpContext ContextoComLimiteDoServidor(long limite)
    {
        DefaultHttpContext context = MiddlewareDeErrosDeTeste.CriarContexto();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new MiddlewareDoLimiteDeTeste.LimiteDoServidor { MaxRequestBodySize = limite });
        return context;
    }
}
