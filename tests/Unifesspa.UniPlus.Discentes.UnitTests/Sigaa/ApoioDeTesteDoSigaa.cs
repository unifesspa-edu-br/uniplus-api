namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

/// <summary>
/// Relógio controlado pelo teste. O projeto não adota biblioteca de relógio falso — cada
/// suíte declara o seu, e este acompanha a convenção.
/// </summary>
internal sealed class RelogioControlado(DateTimeOffset instanteInicial) : TimeProvider
{
    private DateTimeOffset _agora = instanteInicial;

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan quanto) => _agora = _agora.Add(quanto);
}

/// <summary>
/// Substitui a rede nos testes do cliente do SIGAA.
/// </summary>
/// <remarks>
/// Entra como manipulador primário da cadeia de envio, o que preserva intactos os
/// manipuladores reais de resiliência e autenticação — é justamente o comportamento
/// deles que se quer exercitar. Registra cada requisição recebida para que o teste possa
/// afirmar quantas vezes cada endereço foi chamado.
/// </remarks>
internal sealed class RedeSimulada : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
    private readonly ConcurrentQueue<string> _caminhosChamados = new();
    private int _chamadas;

    public RedeSimulada(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>Caminhos chamados, na ordem em que chegaram.</summary>
    public IReadOnlyList<string> CaminhosChamados => [.. _caminhosChamados];

    /// <summary>Quantas vezes o endereço de autenticação foi chamado.</summary>
    public int AutenticacoesRealizadas =>
        CaminhosChamados.Count(caminho => caminho.Contains("authentication_token", StringComparison.Ordinal));

    /// <summary>Cabeçalhos de autorização recebidos, na ordem.</summary>
    public ConcurrentQueue<string?> AutorizacoesRecebidas { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        int indice = Interlocked.Increment(ref _chamadas);
        _caminhosChamados.Enqueue(request.RequestUri?.PathAndQuery ?? string.Empty);
        AutorizacoesRecebidas.Enqueue(request.Headers.Authorization?.Parameter);

        return Task.FromResult(_responder(request, indice));
    }
}

/// <summary>
/// Monta respostas de teste no formato que a origem usa.
/// </summary>
internal static class RespostasDoSigaa
{
    public static HttpResponseMessage ComToken(string token) =>
        Json(HttpStatusCode.OK, $$"""{"token":"{{token}}"}""");

    public static HttpResponseMessage ColecaoVazia() =>
        Json(HttpStatusCode.OK, """{"hydra:member":[],"hydra:totalItems":0}""");

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    public static HttpResponseMessage Json(HttpStatusCode status, string corpo) =>
        new(status)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };

    /// <summary>
    /// Constrói um token no formato que a origem emite, com os instantes de emissão e
    /// expiração declarados. A assinatura é irrelevante: quem a verifica é a origem, e o
    /// cliente só lê os instantes para saber quando renovar.
    /// </summary>
    public static string TokenCom(DateTimeOffset emitidoEm, DateTimeOffset expiraEm, string marca = "t")
    {
        string cabecalho = CodificarSemPreenchimento("""{"alg":"RS512","typ":"JWT"}"""u8);

        string corpo = CodificarSemPreenchimento(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iat = emitidoEm.ToUnixTimeSeconds(),
            exp = expiraEm.ToUnixTimeSeconds(),
            username = "servico",
        })));

        return $"{cabecalho}.{corpo}.assinatura-{marca}";
    }

    /// <summary>Token sem os instantes, para exercitar a validade assumida.</summary>
    public static string TokenSemValidade() =>
        $"{CodificarSemPreenchimento("""{"alg":"RS512"}"""u8)}"
        + $".{CodificarSemPreenchimento("""{"username":"servico"}"""u8)}.assinatura";

    private static string CodificarSemPreenchimento(ReadOnlySpan<byte> conteudo) =>
        Base64Url.EncodeToString(conteudo);
}
