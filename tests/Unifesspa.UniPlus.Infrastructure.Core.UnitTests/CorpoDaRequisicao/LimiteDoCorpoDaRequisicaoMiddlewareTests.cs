namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.CorpoDaRequisicao;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

using static Unifesspa.UniPlus.Infrastructure.Core.UnitTests.CorpoDaRequisicao.MiddlewareDoLimiteDeTeste;

public sealed class LimiteDoCorpoDaRequisicaoMiddlewareTests
{
    private const long TamanhoDeclaradoAcimaDoLimite = 7_777_777;

    [Fact(DisplayName = "O tamanho declarado acima do limite responde 413 com o código do limite, sem chamar o próximo passo")]
    public async Task TamanhoDeclaradoAcimaDoLimite_Responde413SemChamarOProximo()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(TamanhoDeclaradoAcimaDoLimite);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeFalse();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        contexto.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact(DisplayName = "A resposta 413 traz código, título e detalhe com o limite, sem o tamanho declarado")]
    public async Task Resposta413_TrazSoOLimite()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(TamanhoDeclaradoAcimaDoLimite);

        await montagem.Middleware.InvokeAsync(contexto);

        using JsonDocument resposta = await LerRespostaAsync(contexto);
        resposta.RootElement.GetProperty("code").GetString().Should().Be("uniplus.requisicao.corpo_acima_do_limite");
        resposta.RootElement.GetProperty("title").GetString().Should().Be("O corpo da requisição passa do tamanho aceito");
        resposta.RootElement.GetProperty("detail").GetString().Should().Be(
            "O corpo da requisição passa do limite de 100 bytes aceito por esta operação. Reduza o conteúdo enviado.");
        resposta.RootElement.GetRawText().Should().NotContain(TamanhoDeclaradoAcimaDoLimite.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "O corpo com exatamente o tamanho do limite segue para o próximo passo")]
    public async Task TamanhoExatamenteNoLimite_Segue()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(LimiteGeral);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeTrue();
        contexto.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "Um byte acima do limite é recusado")]
    public async Task UmByteAcimaDoLimite_Recusa()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(LimiteGeral + 1);

        await montagem.Middleware.InvokeAsync(contexto);

        contexto.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact(DisplayName = "Sem tamanho declarado, segue e grava o limite no recurso do servidor")]
    public async Task SemTamanhoDeclarado_SegueEGravaOLimiteNoServidor()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(tamanhoDeclarado: null);
        LimiteDoServidor servidor = new();
        contexto.Features.Set<IHttpMaxRequestBodySizeFeature>(servidor);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeTrue();
        servidor.MaxRequestBodySize.Should().Be(LimiteGeral);
    }

    [Fact(DisplayName = "O recurso do servidor que não aceita escrita não derruba o pedido nem muda o limite dele")]
    public async Task RecursoSemEscrita_NaoFalha()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(tamanhoDeclarado: null);
        LimiteDoServidor servidor = new() { IsReadOnly = true, MaxRequestBodySize = 5 };
        contexto.Features.Set<IHttpMaxRequestBodySizeFeature>(servidor);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeTrue();
        servidor.MaxRequestBodySize.Should().Be(5);
    }

    [Fact(DisplayName = "Sem o recurso do servidor, o tamanho declarado ainda é conferido")]
    public async Task SemRecursoDoServidor_ConferaOTamanhoDeclarado()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(LimiteGeral + 1);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeFalse();
    }

    [Fact(DisplayName = "O limite da operação maior que o geral vale: o corpo entre os dois segue e o servidor recebe o da operação")]
    public async Task LimiteDaOperacaoMaior_Vale()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(LimiteGeral + 200);
        DeclararLimiteDaOperacao(contexto, LimiteGeral + 400);
        LimiteDoServidor servidor = new();
        contexto.Features.Set<IHttpMaxRequestBodySizeFeature>(servidor);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeTrue();
        servidor.MaxRequestBodySize.Should().Be(LimiteGeral + 400);
    }

    [Fact(DisplayName = "O limite da operação menor que o geral vale: o corpo entre os dois é recusado com o limite da operação")]
    public async Task LimiteDaOperacaoMenor_Vale()
    {
        MiddlewareDoLimiteDeTeste montagem = new();
        DefaultHttpContext contexto = Contexto(LimiteGeral - 10);
        DeclararLimiteDaOperacao(contexto, LimiteGeral - 50);

        await montagem.Middleware.InvokeAsync(contexto);

        montagem.ProximoChamado.Should().BeFalse();
        using JsonDocument resposta = await LerRespostaAsync(contexto);
        resposta.RootElement.GetProperty("detail").GetString().Should().Contain("limite de 50 bytes");
    }

    [Fact(DisplayName = "O registro da recusa traz só o limite e o caminho, sem o tamanho declarado")]
    public async Task RegistroDaRecusa_TrazSoOLimiteEOCaminho()
    {
        MiddlewareDoLimiteDeTeste montagem = new();

        await montagem.Middleware.InvokeAsync(Contexto(TamanhoDeclaradoAcimaDoLimite));

        montagem.Registro.Entradas.Should().ContainSingle().Which.Should().Be(
            (LogLevel.Warning, "Corpo da requisição acima do limite de 100 bytes em /teste/rota"));
    }
}
