namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// <b>Valor de rota ou query que não converte responde no mesmo envelope que todo o resto.</b>
/// </summary>
/// <remarks>
/// <para>
/// A recusa de conversão é a primeira coisa que um cliente encontra ao errar um endereço, e era
/// a única que continuava saindo no envelope do framework: sem <c>code</c>, sem <c>traceId</c>,
/// com título em inglês. Quem integra recebia, do mesmo servidor, dois formatos de erro
/// diferentes conforme onde errou.
/// </para>
/// <para>
/// A conferência corre contra o <b>pipeline HTTP real</b>, e não sobre um <c>ModelState</c>
/// montado à mão, porque é exatamente aí que a distinção vive: o que separa conversão de
/// validação é <b>quando</b> o erro entra no <c>ModelState</c> — durante o binding, ou depois
/// dele. Um <c>ModelState</c> preenchido pelo teste tem as duas coisas indistinguíveis, e um
/// teste assim aprova código que o pipeline nunca executa.
/// </para>
/// </remarks>
[Collection(MonolitoHostCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class ConversaoDeRotaEQueryTests
{
    private readonly MonolitoPostgresFixture _fixture;

    public ConversaoDeRotaEQueryTests(MonolitoPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory(DisplayName = "Valor de query que não converte responde no envelope canônico")]
    [InlineData("/api/publicacoes/tipos-ato?vigentes=abc")]
    [InlineData("/api/publicacoes/tipos-ato/EDITAL_ABERTURA/vigente?data=abc")]
    [InlineData("/api/organizacao/unidades?tipo=abc")]
    [InlineData("/api/organizacao/unidades?tipo[0]=abc")]
    [InlineData("/api/organizacao/unidades?tipo=abc&tipo=def")]
    [InlineData("/api/selecao/certames?situacao=abc")]
    public async Task Ler_QuandoValorDeQueryNaoConverte_DeveResponderNoEnvelopeCanonico(string endereco)
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(new Uri(endereco, UriKind.Relative), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        JsonElement corpo = await Corpo(resposta);

        corpo.TryGetProperty("code", out JsonElement code).Should().BeTrue(
            "o catálogo de erros é o que o cliente programa contra — sem `code` ele não tem o que ler");
        code.GetString().Should().NotBeNullOrWhiteSpace();

        corpo.TryGetProperty("traceId", out JsonElement traceId).Should().BeTrue(
            "sem o traceId o suporte não liga a queixa do cliente ao registro do servidor");
        traceId.GetString().Should().NotBeNullOrWhiteSpace();

        string detail = corpo.GetProperty("detail").GetString()!;

        detail.Should().NotContain("abc",
            "devolver o valor recusado dentro da mensagem é o que transforma a resposta de erro em superfície de " +
            "reflexão de conteúdo do cliente");
        detail.Should().NotContain("obrigatório",
            "os dois parâmetros são opcionais, e anunciá-los como obrigatórios manda o cliente procurar defeito " +
            "onde não há — o que faltava era o valor caber no tipo, não a declaração do parâmetro");
    }

    [Fact(DisplayName = "A orientação de formato do parâmetro de rota continua chegando ao cliente")]
    public async Task Ler_QuandoParametroDeRotaReprovaNoFormato_DeveManterAMensagemQueEnsinaACorrigir()
    {
        // O outro lado da mudança, e o que ela não pode atropelar: aqui a conversão DEU CERTO —
        // o código é texto e chegou como texto — e quem reprovou foi a expressão regular, cuja
        // mensagem diz qual é o formato esperado. Trocá-la por uma recusa genérica tiraria do
        // cliente a única informação que o faz acertar na segunda tentativa.
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/publicacoes/tipos-ato/edital_minusculo/vigente", UriKind.Relative), CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string texto = await resposta.Content.ReadAsStringAsync(CancellationToken.None);

        texto.Should().Contain("letras maiúsculas sem acento",
            "é a mensagem escrita para este parâmetro, e é ela que ensina o cliente a corrigir");
    }

    [Fact(DisplayName = "Conversão e validação falhando juntas preservam a orientação específica")]
    public async Task Ler_QuandoConversaoEValidacaoFalhamJuntas_DeveManterAOrientacaoEspecifica()
    {
        // A requisição erra as duas coisas: o código da rota não casa com o formato, e a data da
        // query não vira data. Uma recusa genérica de conversão engoliria a única frase que
        // ensina a corrigir. Entre afirmar pouco e apagar orientação, a regra é responder por
        // quem tem mais a dizer — e aqui quem tem mais a dizer é a validação.
        using HttpClient client = _fixture.Factory.CreateClient();

        using HttpResponseMessage resposta = await client.GetAsync(
            new Uri("/api/publicacoes/tipos-ato/edital_minusculo/vigente?data=abc", UriKind.Relative),
            CancellationToken.None);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string texto = await resposta.Content.ReadAsStringAsync(CancellationToken.None);

        texto.Should().Contain("letras maiúsculas sem acento",
            "a mensagem do parâmetro reprovado não pode sumir porque OUTRO parâmetro, na mesma requisição, " +
            "falhou na conversão");
    }

    private static async Task<JsonElement> Corpo(HttpResponseMessage resposta)
    {
        string texto = await resposta.Content.ReadAsStringAsync(CancellationToken.None);
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
