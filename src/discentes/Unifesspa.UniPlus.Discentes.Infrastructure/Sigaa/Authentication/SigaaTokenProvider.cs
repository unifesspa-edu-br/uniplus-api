namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

using System;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Guarda o token de acesso à API do SIGAA e o renova quando vence.
/// </summary>
/// <remarks>
/// <para>
/// A origem autentica por usuário e senha em JSON e devolve apenas o token, sem dizer
/// quanto ele dura. A validade é lida do próprio token, que a carrega — e essa leitura
/// serve exclusivamente para agendar a renovação. Quem decide se o token vale é a
/// origem, a cada requisição; um valor adulterado aqui só provocaria uma renovação
/// desnecessária ou uma recusa que o cliente já sabe tratar.
/// </para>
/// <para>
/// Renovações concorrentes são serializadas: a primeira requisição que encontra o token
/// vencido renova, e as demais aguardam e reaproveitam o resultado.
/// </para>
/// </remarks>
internal sealed partial class SigaaTokenProvider : IDisposable
{
    /// <summary>
    /// Nome do cliente que fala com o endereço de autenticação. É um cliente à parte:
    /// carimbar a própria autenticação com um token seria circular, e a instabilidade da
    /// consulta não pode bloquear a renovação.
    /// </summary>
    internal const string NomeDoClienteHttp = "sigaa-autenticacao";

    private readonly IHttpClientFactory _clientes;
    private readonly SigaaOptions _opcoes;
    private readonly ILogger<SigaaTokenProvider> _logger;
    private readonly TimeProvider _relogio;
    private readonly SemaphoreSlim _trava = new(initialCount: 1, maxCount: 1);

    private string? _token;
    private DateTimeOffset _expiraEm;
    private bool _descartado;

    public SigaaTokenProvider(
        IHttpClientFactory clientes,
        IOptions<SigaaOptions> opcoes,
        ILogger<SigaaTokenProvider> logger,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(clientes);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(relogio);

        _clientes = clientes;
        _opcoes = opcoes.Value;
        _logger = logger;
        _relogio = relogio;
    }

    /// <summary>
    /// Devolve um token válido, renovando-o se o atual já venceu ou está prestes a vencer.
    /// </summary>
    public async Task<string> ObterAsync(CancellationToken cancellationToken)
    {
        if (TentarReaproveitar(out string? vigente))
        {
            return vigente;
        }

        await _trava.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Outra requisição pode ter renovado enquanto esperávamos pela trava.
            if (TentarReaproveitar(out string? renovadoPorOutra))
            {
                return renovadoPorOutra;
            }

            string novo = await AutenticarAsync(cancellationToken).ConfigureAwait(false);
            TimeSpan validade = CalcularValidade(novo);

            _token = novo;
            _expiraEm = _relogio.GetUtcNow().Add(validade);

            LogTokenRenovado(_logger, _opcoes.Usuario, (int)validade.TotalSeconds);

            return novo;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>
    /// Descarta o token que a origem recusou, para que a próxima requisição renove.
    /// </summary>
    /// <remarks>
    /// Só descarta se o token recusado ainda for o que está guardado. Sem essa condição,
    /// várias requisições que saíram juntas com o mesmo token vencido apagariam, uma após
    /// a outra, o token que a primeira delas acabou de renovar — e cada uma renovaria de
    /// novo, transformando uma renovação em tantas quantas estivessem em voo.
    /// </remarks>
    public void Descartar(string tokenRecusado)
    {
        _trava.Wait();
        try
        {
            if (string.Equals(_token, tokenRecusado, StringComparison.Ordinal))
            {
                _token = null;
                _expiraEm = default;
            }
        }
        finally
        {
            _trava.Release();
        }
    }

    private bool TentarReaproveitar([NotNullWhen(true)] out string? token)
    {
        string? guardado = _token;
        TimeSpan margem = TimeSpan.FromSeconds(_opcoes.MargemDeRenovacaoDoTokenEmSegundos);

        if (guardado is not null && _relogio.GetUtcNow() < _expiraEm - margem)
        {
            token = guardado;
            return true;
        }

        token = null;
        return false;
    }

    private async Task<string> AutenticarAsync(CancellationToken cancellationToken)
    {
        // O cliente é pedido à fábrica a cada autenticação, e não guardado junto com o
        // token: guardá-lo prenderia para sempre o mesmo canal de rede, contornando a
        // rotação periódica que a fábrica faz para que mudanças de endereço do servidor
        // sejam percebidas.
        using HttpClient cliente = _clientes.CreateClient(NomeDoClienteHttp);

        using HttpResponseMessage resposta = await cliente
            .PostAsJsonAsync(
                "authentication_token",
                new PedidoDeAutenticacao(_opcoes.Usuario, _opcoes.Senha),
                cancellationToken)
            .ConfigureAwait(false);

        if (!resposta.IsSuccessStatusCode)
        {
            int status = (int)resposta.StatusCode;

            // O corpo da recusa não é registrado: ele ecoa o que foi enviado, incluindo o
            // usuário, e num erro da origem pode trazer dado de quem está sendo consultado.
            LogAutenticacaoRecusada(_logger, _opcoes.Usuario, status);

            throw new SigaaAutenticacaoException(
                $"A API do SIGAA recusou a autenticação do usuário de serviço com status {status}.");
        }

        RespostaDeAutenticacao? conteudo = await resposta.Content
            .ReadFromJsonAsync<RespostaDeAutenticacao>(cancellationToken)
            .ConfigureAwait(false);

        if (conteudo is null || string.IsNullOrWhiteSpace(conteudo.Token))
        {
            throw new SigaaAutenticacaoException(
                "A API do SIGAA respondeu à autenticação sem devolver um token.");
        }

        return conteudo.Token;
    }

    /// <summary>
    /// Descobre por quanto tempo o token vale.
    /// </summary>
    /// <remarks>
    /// Prefere a diferença entre a expiração e a emissão declaradas no token, porque as
    /// duas vêm do relógio da origem: a subtração cancela qualquer diferença entre aquele
    /// relógio e o desta máquina. Só quando a emissão não está declarada é que a expiração
    /// é comparada com o relógio local, aí sim sujeita a essa diferença. Sem nenhum dos
    /// dois, vale a validade assumida na configuração. O resultado é sempre limitado pelo
    /// teto configurado.
    /// </remarks>
    private TimeSpan CalcularValidade(string token)
    {
        TimeSpan teto = TimeSpan.FromSeconds(_opcoes.ValidadeMaximaDoTokenEmSegundos);
        TimeSpan assumida = TimeSpan.FromSeconds(_opcoes.ValidadeAssumidaDoTokenEmSegundos);

        if (!TentarLerInstantes(token, out long expiraEm, out long? emitidoEm))
        {
            LogValidadeIndisponivel(_logger);
            return Min(assumida, teto);
        }

        // Os instantes vêm de fora e podem estar em qualquer escala — uma expiração
        // gravada em milissegundos, por exemplo, estoura a faixa de datas representáveis.
        // Isso é mais um caso de validade ilegível, e não pode impedir o token de chegar
        // à origem: quem o valida é ela.
        long segundos;
        if (emitidoEm is { } emissao)
        {
            if (!EstaNaFaixaDeDatas(expiraEm) || !EstaNaFaixaDeDatas(emissao))
            {
                LogValidadeIndisponivel(_logger);
                return Min(assumida, teto);
            }

            segundos = expiraEm - emissao;
        }
        else
        {
            if (!EstaNaFaixaDeDatas(expiraEm))
            {
                LogValidadeIndisponivel(_logger);
                return Min(assumida, teto);
            }

            segundos = (long)(DateTimeOffset.FromUnixTimeSeconds(expiraEm) - _relogio.GetUtcNow())
                .TotalSeconds;
        }

        if (segundos <= 0)
        {
            return Min(assumida, teto);
        }

        // Comparar em segundos antes de converter evita estourar a conversão com um
        // intervalo grande demais para ser representado.
        long tetoEmSegundos = _opcoes.ValidadeMaximaDoTokenEmSegundos;
        return TimeSpan.FromSeconds(Math.Min(segundos, tetoEmSegundos));
    }

    /// <summary>
    /// Lê os instantes de expiração e emissão declarados no token, sem verificar a
    /// assinatura — a verificação é da origem, e o que se busca aqui é só o momento de
    /// renovar.
    /// </summary>
    private static bool TentarLerInstantes(string token, out long expiraEm, out long? emitidoEm)
    {
        expiraEm = 0;
        emitidoEm = null;

        ReadOnlySpan<char> restante = token;
        int primeiroPonto = restante.IndexOf('.');
        if (primeiroPonto < 0)
        {
            return false;
        }

        ReadOnlySpan<char> aposCabecalho = restante[(primeiroPonto + 1)..];
        int segundoPonto = aposCabecalho.IndexOf('.');
        ReadOnlySpan<char> corpo = segundoPonto < 0 ? aposCabecalho : aposCabecalho[..segundoPonto];

        if (corpo.IsEmpty)
        {
            return false;
        }

        try
        {
            byte[] bytes = new byte[Base64Url.GetMaxDecodedLength(corpo.Length)];

            // Apesar do nome, este método lança quando encontra caractere fora do
            // alfabeto — o valor de retorno cobre só o caso de destino pequeno demais.
            // Sem esta captura, um token que não seja um dos três trechos separados por
            // ponto derrubaria a autenticação inteira, em vez de apenas deixar a validade
            // indisponível.
            if (!Base64Url.TryDecodeFromChars(corpo, bytes, out int escritos))
            {
                return false;
            }

            using JsonDocument documento = JsonDocument.Parse(bytes.AsMemory(0, escritos));

            if (!documento.RootElement.TryGetProperty("exp", out JsonElement expiracao)
                || !expiracao.TryGetInt64(out expiraEm))
            {
                return false;
            }

            if (documento.RootElement.TryGetProperty("iat", out JsonElement emissao)
                && emissao.TryGetInt64(out long lidoEmitidoEm))
            {
                emitidoEm = lidoEmitidoEm;
            }

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Diz se o instante, em segundos desde a época, cabe na faixa de datas que se
    /// consegue representar.
    /// </summary>
    private static bool EstaNaFaixaDeDatas(long segundosDesdeAEpoca) =>
        segundosDesdeAEpoca >= DateTimeOffset.MinValue.ToUnixTimeSeconds()
        && segundosDesdeAEpoca <= DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    public void Dispose()
    {
        if (_descartado)
        {
            return;
        }

        _trava.Dispose();
        _descartado = true;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Token de acesso ao SIGAA renovado. Usuario={Usuario} ValidadeEmSegundos={ValidadeEmSegundos}")]
    private static partial void LogTokenRenovado(ILogger logger, string usuario, int validadeEmSegundos);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A API do SIGAA recusou a autenticação do usuário de serviço. Usuario={Usuario} Status={Status}")]
    private static partial void LogAutenticacaoRecusada(ILogger logger, string usuario, int status);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "O token do SIGAA não declara validade legível; a renovação usará a validade assumida na configuração.")]
    private static partial void LogValidadeIndisponivel(ILogger logger);

    private sealed record PedidoDeAutenticacao(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("password")] string Password);

    private sealed record RespostaDeAutenticacao
    {
        [JsonPropertyName("token")]
        public string Token { get; init; } = string.Empty;
    }
}
