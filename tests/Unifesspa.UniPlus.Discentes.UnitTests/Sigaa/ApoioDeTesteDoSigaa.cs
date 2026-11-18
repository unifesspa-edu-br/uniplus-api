namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;

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

    /// <summary>
    /// Token com um valor de expiração arbitrário e sem emissão, para exercitar o que
    /// acontece quando a origem declara um instante que não cabe numa data.
    /// </summary>
    public static string TokenComExpiracaoBruta(long expiracao)
    {
        string cabecalho = CodificarSemPreenchimento("""{"alg":"RS512","typ":"JWT"}"""u8);
        string corpo = CodificarSemPreenchimento(
            Encoding.UTF8.GetBytes($$"""{"exp":{{expiracao}},"username":"servico"}"""));

        return $"{cabecalho}.{corpo}.assinatura";
    }

    /// <summary>Token sem os instantes, para exercitar a validade assumida.</summary>
    public static string TokenSemValidade() =>
        $"{CodificarSemPreenchimento("""{"alg":"RS512"}"""u8)}"
        + $".{CodificarSemPreenchimento("""{"username":"servico"}"""u8)}.assinatura";

    /// <summary>
    /// Token cujo trecho do meio é exatamente o JSON informado, qualquer que seja o seu
    /// formato — inclusive os que a origem não emitiria.
    /// </summary>
    public static string TokenComCorpo(string corpoJson) =>
        $"{CodificarSemPreenchimento("""{"alg":"RS512"}"""u8)}.{CodificarCorpo(corpoJson)}.assinatura";

    /// <summary>O JSON informado, codificado como o trecho do meio de um token.</summary>
    public static string CodificarCorpo(string corpoJson) =>
        CodificarSemPreenchimento(Encoding.UTF8.GetBytes(corpoJson));

    private static string CodificarSemPreenchimento(ReadOnlySpan<byte> conteudo) =>
        Base64Url.EncodeToString(conteudo);
}

/// <summary>
/// As chaves de configuração do SIGAA, relativas à seção <c>Sigaa</c>.
/// </summary>
internal static class ChavesDoSigaa
{
    public const string BaseUrl = nameof(SigaaOptions.BaseUrl);
    public const string Usuario = nameof(SigaaOptions.Usuario);
    public const string Senha = nameof(SigaaOptions.Senha);
    public const string ItensPorPagina = $"{nameof(SigaaOptions.Paginacao)}:{nameof(PaginacaoDoSigaa.ItensPorPagina)}";
    public const string GrauDeParalelismo = $"{nameof(SigaaOptions.Paginacao)}:{nameof(PaginacaoDoSigaa.GrauDeParalelismo)}";
    public const string ValidadeAssumidaDoToken = $"{nameof(SigaaOptions.Token)}:{nameof(ParametrosDoTokenDoSigaa.ValidadeAssumida)}";
    public const string ValidadeMaximaDoToken = $"{nameof(SigaaOptions.Token)}:{nameof(ParametrosDoTokenDoSigaa.ValidadeMaxima)}";
    public const string MargemDeRenovacaoDoToken = $"{nameof(SigaaOptions.Token)}:{nameof(ParametrosDoTokenDoSigaa.MargemDeRenovacao)}";
    public const string TimeoutPorTentativa = $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.TimeoutPorTentativa)}";
    public const string TimeoutTotal = $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.TimeoutTotal)}";
    public const string MaximoDeRetentativas = $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.MaximoDeRetentativas)}";
    public const string EsperaBaseEntreTentativas = $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.EsperaBaseEntreTentativas)}";
    public const string ChamadasMinimasParaAvaliarInterrupcao =
        $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.ChamadasMinimasParaAvaliarInterrupcao)}";
    public const string JanelaDeAmostragemDaInterrupcao =
        $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.JanelaDeAmostragemDaInterrupcao)}";
    public const string ProporcaoDeFalhaParaInterromper =
        $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.ProporcaoDeFalhaParaInterromper)}";
    public const string DuracaoDaInterrupcao = $"{nameof(SigaaOptions.Resiliencia)}:{nameof(ResilienciaDoSigaa.DuracaoDaInterrupcao)}";
}

/// <summary>
/// Configuração do SIGAA como o operador a entrega: chaves de texto lidas pelo binder, e
/// não propriedades preenchidas à mão. Parte de uma configuração válida e deixa o teste
/// trocar só o que quer provar.
/// </summary>
internal static class OpcoesDoSigaa
{
    private static readonly Dictionary<string, string?> Validas = new()
    {
        [Chave(ChavesDoSigaa.BaseUrl)] = "https://sigaa.exemplo.test",
        [Chave(ChavesDoSigaa.Usuario)] = "servico",
        [Chave(ChavesDoSigaa.Senha)] = "segredo",
    };

    public static SigaaOptions Com(params (string Caminho, string? Valor)[] alteracoes)
    {
        Dictionary<string, string?> valores = new(Validas);

        foreach ((string caminho, string? valor) in alteracoes)
        {
            valores[Chave(caminho)] = valor;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .Build()
            .GetSection(SigaaOptions.SectionName)
            .Get<SigaaOptions>() ?? new SigaaOptions();
    }

    /// <summary>A chave completa de configuração, a partir do caminho dentro da seção.</summary>
    public static string Chave(string caminho) => $"{SigaaOptions.SectionName}:{caminho}";

    /// <summary>Uma duração no texto que o operador escreve, <c>hh:mm:ss</c>.</summary>
    public static string Segundos(double segundos) =>
        TimeSpan.FromSeconds(segundos).ToString("c", System.Globalization.CultureInfo.InvariantCulture);
}
