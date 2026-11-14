namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Cors;

using AwesomeAssertions;

using Microsoft.AspNetCore.Cors.Infrastructure;

public sealed class CorsConfigurationTests
{
    [Fact]
    public void Policy_QuandoAllowAnyHeaderFalso_DeveDeclararIdempotencyKey()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.Headers.Should().Contain("Idempotency-Key",
            because: "endpoints com [RequiresIdempotencyKey] dependem desse header no preflight CORS (ADR-0027). " +
                     "Sem isto na lista, navegadores rejeitam toda criação via SPA antes da request real sair.");
    }

    [Fact]
    public void Policy_QuandoAllowAnyHeaderFalso_DeveDeclararCondicionaisHttp()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.Headers.Should().Contain("If-Match");
        policy.Headers.Should().Contain("If-None-Match");
    }

    [Fact]
    public void Policy_QuandoAllowAnyHeaderFalso_DeveDeclararBasicosRest()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.Headers.Should().Contain(["Content-Type", "Authorization", "Accept", "X-Requested-With"]);
    }

    [Fact]
    public void Policy_QuandoAllowAnyHeaderTrue_PreservaSemantica()
    {
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: true);

        policy.Headers.Should().Equal(["*"],
            because: "AllowAnyHeader=true registra wildcard único na lista (semântica do framework AspNetCore).");
    }

    [Fact]
    public void Policy_QuandoPoliticaPadrao_DeveExporLinkEXPageSize()
    {
        // Uma resposta de origem cruzada só entrega ao JavaScript sete headers considerados
        // seguros, e nenhum dos nossos é um deles. A navegação por cursor vive INTEIRAMENTE em
        // Link e X-Page-Size: sem expô-los, o SPA recebe a página e não tem como pedir a seguinte
        // — o corpo é um array puro, e o endereço de continuação só existe no header.
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.ExposedHeaders.Should().Contain(["Link", "X-Page-Size"],
            because: "sem eles, response.headers.get() devolve null e a paginação some para quem "
                + "consome do navegador — sem erro, sem status diferente e sem nada a investigar.");
    }

    [Fact]
    public void Policy_QuandoPoliticaPadrao_DeveExporXTotalCount()
    {
        // O total da coleção sai da mesma extensão de paginação que emite Link e X-Page-Size, e
        // não pertence a módulo nenhum. Sem expô-lo, a tela pede include_total=true, recebe 200 e
        // o contador fica vazio — o header chega ao navegador e o JavaScript lê null.
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.ExposedHeaders.Should().Contain("X-Total-Count",
            because: "quem pede a contagem precisa conseguir lê-la de outra origem.");
    }

    [Fact]
    public void Policy_QuandoPoliticaPadrao_DeveManterSeloEReplayExpostos()
    {
        // Os dois que já estavam na lista. Entram aqui para que uma mudança futura não os remova
        // sem ninguém perceber: o ETag é a precondição da próxima mutação, e sem lê-lo toda edição
        // sob retificação sairia 428 no navegador.
        CorsPolicy policy = BuildDefaultPolicy(allowAnyHeader: false);

        policy.ExposedHeaders.Should().Contain(["ETag", "Idempotency-Replayed"]);
    }

    private static CorsPolicy BuildDefaultPolicy(bool allowAnyHeader) =>
        PoliticaCorsDeTeste.Construir(allowAnyHeader);
}
