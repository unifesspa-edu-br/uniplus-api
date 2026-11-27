namespace Unifesspa.UniPlus.Host.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// O limite do corpo da requisição vale antes da autenticação e da autorização, para quem chama sem login e para quem chama sem
/// permissão, e a recusa não repete nada do corpo recebido.
/// </summary>
[Collection(MonolitoHostCollection.Name)]
public sealed class LimiteDoCorpoDaRequisicaoNoHostTests(MonolitoPostgresFixture fixture)
{
    private const string Codigo = "uniplus.requisicao.corpo_acima_do_limite";
    private const string OperacaoComLimiteGeral = "/api/configuracao/admin/campi";
    private const string OrigemDoSpa = "http://localhost:4200";
    private const long LimiteDoCorpoDoInepEmBytes = (10L * 1024 * 1024) + (64L * 1024);

    [Fact(DisplayName = "Quem chama sem login e envia corpo acima do limite recebe 413, e não 401")]
    public async Task SemLogin_CorpoAcimaDoLimite_Responde413()
    {
        using HttpResponseMessage resposta = await EnviarAsync(OperacaoComLimiteGeral, JsonComTamanho(LimiteDoCorpoDoPedido.Bytes + 1));

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await AfirmarCodigoAsync(resposta);
    }

    [Fact(DisplayName = "Quem chama com login e sem permissão e envia corpo acima do limite recebe 413, e não 403")]
    public async Task SemPermissao_CorpoAcimaDoLimite_Responde413()
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            OperacaoComLimiteGeral, JsonComTamanho(LimiteDoCorpoDoPedido.Bytes + 1), autenticado: true);

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await AfirmarCodigoAsync(resposta);
    }

    [Fact(DisplayName = "O corpo com exatamente o limite segue para a autenticação, e um byte a mais é recusado")]
    public async Task NoLimiteExato_Segue_EUmByteAMais_Recusa()
    {
        using HttpResponseMessage noLimite = await EnviarAsync(OperacaoComLimiteGeral, JsonComTamanho(LimiteDoCorpoDoPedido.Bytes));
        using HttpResponseMessage acima = await EnviarAsync(OperacaoComLimiteGeral, JsonComTamanho(LimiteDoCorpoDoPedido.Bytes + 1));

        noLimite.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        acima.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact(DisplayName = "O pedido grande cheio de itens inválidos recebe uma única recusa 413, sem lista de itens")]
    public async Task PedidoGrandeComItensInvalidos_RecebeUmaUnicaRecusa()
    {
        string itensInvalidos = $"[{string.Concat(Enumerable.Repeat("{\"codigo\":1},", (int)(LimiteDoCorpoDoPedido.Bytes / 13) + 1))}{{}}]";

        using HttpResponseMessage resposta = await EnviarAsync(OperacaoComLimiteGeral, itensInvalidos, autenticado: true);

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        using JsonDocument problema = await AfirmarCodigoAsync(resposta);
        problema.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "A resposta 413 não repete nenhum trecho do corpo recebido")]
    public async Task Resposta413_NaoRepeteOCorpo()
    {
        const string PessoaFicticia = "Ana Beatriz Lima Ficticia";
        string corpo = $"{{\"nome\":\"{PessoaFicticia}\",\"preenchimento\":\"{new string('x', (int)LimiteDoCorpoDoPedido.Bytes)}\"}}";

        using HttpResponseMessage resposta = await EnviarAsync(OperacaoComLimiteGeral, corpo);

        (await resposta.Content.ReadAsStringAsync()).Should().NotContain(PessoaFicticia).And.NotContain("preenchimento");
    }

    [Fact(DisplayName = "A recusa 413 de origem permitida leva o cabeçalho de CORS")]
    public async Task Resposta413_LevaOCabecalhoDeCors()
    {
        using HttpResponseMessage resposta = await EnviarAsync(
            OperacaoComLimiteGeral, JsonComTamanho(LimiteDoCorpoDoPedido.Bytes + 1), origem: OrigemDoSpa);

        resposta.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        resposta.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(OrigemDoSpa);
    }

    [Fact(DisplayName = "O envio do arquivo do INEP aceita corpo maior que o limite geral, até 10 MiB mais 64 KiB, e recusa um byte além")]
    public async Task EnvioDoArquivoDoInep_TemLimiteProprio()
    {
        string caminho = $"/api/selecao/processos-seletivos/{Guid.CreateVersion7()}/etapas/{Guid.CreateVersion7()}/edicoes-do-enem/2024/lotes-de-notas";

        using HttpResponseMessage acimaDoGeral = await EnviarAsync(caminho, new byte[LimiteDoCorpoDoPedido.Bytes + 1], "multipart/form-data");
        using HttpResponseMessage noLimiteDoInep = await EnviarAsync(caminho, new byte[LimiteDoCorpoDoInepEmBytes], "multipart/form-data");
        using HttpResponseMessage acimaDoInep = await EnviarAsync(caminho, new byte[LimiteDoCorpoDoInepEmBytes + 1], "multipart/form-data");

        acimaDoGeral.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        noLimiteDoInep.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        acimaDoInep.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await AfirmarCodigoAsync(acimaDoInep)).Dispose();
    }

    private static string JsonComTamanho(long bytes) => $"{{\"a\":\"{new string('x', (int)bytes - 8)}\"}}";

    private static async Task<JsonDocument> AfirmarCodigoAsync(HttpResponseMessage resposta)
    {
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be(Codigo);
        return problema;
    }

    private Task<HttpResponseMessage> EnviarAsync(string caminho, string corpo, bool autenticado = false, string? origem = null) =>
        EnviarAsync(caminho, Encoding.UTF8.GetBytes(corpo), "application/json", autenticado, origem);

    private async Task<HttpResponseMessage> EnviarAsync(
        string caminho, byte[] corpo, string tipoDoConteudo, bool autenticado = false, string? origem = null)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage pedido = MontarPedido(caminho, corpo, tipoDoConteudo, autenticado, origem);

        return await client.SendAsync(pedido);
    }

    private static HttpRequestMessage MontarPedido(string caminho, byte[] corpo, string tipoDoConteudo, bool autenticado, string? origem)
    {
        HttpRequestMessage pedido = new(HttpMethod.Post, new Uri(caminho, UriKind.Relative)) { Content = new ByteArrayContent(corpo) };
        pedido.Content.Headers.ContentType = new MediaTypeHeaderValue(tipoDoConteudo);
        if (autenticado)
        {
            pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
            pedido.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, "pessoa.sem.permissao");
        }

        if (origem is not null)
        {
            pedido.Headers.Add("Origin", origem);
        }

        return pedido;
    }
}
