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
    private readonly SemaphoreSlim _acessoExclusivo = new(initialCount: 1, maxCount: 1);

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

        await _acessoExclusivo.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReaproveitarOuRenovarAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _acessoExclusivo.Release();
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
        _acessoExclusivo.Wait();
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
            _acessoExclusivo.Release();
        }
    }

    /// <summary>
    /// Outra requisição pode ter renovado enquanto esta esperava o acesso exclusivo; só se o token
    /// guardado ainda não serve é que se autentica de novo.
    /// </summary>
    private async Task<string> ReaproveitarOuRenovarAsync(CancellationToken cancellationToken)
    {
        if (TentarReaproveitar(out string? renovadoPorOutra))
        {
            return renovadoPorOutra;
        }

        string novo = await AutenticarAsync(cancellationToken).ConfigureAwait(false);
        TimeSpan validade = CalcularValidade(novo);

        Guardar(novo, validade);

        return novo;
    }

    private void Guardar(string token, TimeSpan validade)
    {
        _token = token;
        _expiraEm = _relogio.GetUtcNow().Add(validade);

        LogTokenRenovado(_logger, _opcoes.Usuario, (int)validade.TotalSeconds);
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

    /// <summary>
    /// Autentica o usuário de serviço e devolve o token que a origem entregou.
    /// </summary>
    /// <remarks>
    /// O cliente é pedido à fábrica a cada autenticação, e não guardado junto com o token:
    /// guardá-lo prenderia para sempre o mesmo canal de rede, contornando a rotação
    /// periódica que a fábrica faz para que mudanças de endereço do servidor sejam
    /// percebidas.
    /// </remarks>
    private async Task<string> AutenticarAsync(CancellationToken cancellationToken)
    {
        using HttpClient cliente = _clientes.CreateClient(NomeDoClienteHttp);

        using HttpResponseMessage resposta = await cliente
            .PostAsJsonAsync(
                "authentication_token",
                new PedidoDeAutenticacao(_opcoes.Usuario, _opcoes.Senha),
                cancellationToken)
            .ConfigureAwait(false);

        ExigirAutenticacaoAceita(resposta);

        return await LerTokenAsync(resposta, cancellationToken).ConfigureAwait(false);
    }

    private void ExigirAutenticacaoAceita(HttpResponseMessage resposta)
    {
        if (!resposta.IsSuccessStatusCode)
        {
            int status = (int)resposta.StatusCode;

            // O corpo da recusa não é registrado: ele ecoa o que foi enviado, incluindo o
            // usuário, e num erro da origem pode trazer dado de quem está sendo consultado.
            LogAutenticacaoRecusada(_logger, _opcoes.Usuario, status);

            throw new SigaaAutenticacaoException(
                $"A API do SIGAA recusou a autenticação do usuário de serviço com status {status}.");
        }
    }

    private static async Task<string> LerTokenAsync(
        HttpResponseMessage resposta,
        CancellationToken cancellationToken)
    {
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
    /// limite configurado.
    /// </remarks>
    private TimeSpan CalcularValidade(string token)
    {
        TimeSpan assumida = TimeSpan.FromSeconds(
            Math.Min(_opcoes.ValidadeAssumidaDoTokenEmSegundos, _opcoes.ValidadeMaximaDoTokenEmSegundos));

        if (LerValidadeDeclarada(token) is not { } segundos)
        {
            LogValidadeIndisponivel(_logger);
            return assumida;
        }

        return segundos > 0
            ? TimeSpan.FromSeconds(Math.Min(segundos, _opcoes.ValidadeMaximaDoTokenEmSegundos))
            : assumida;
    }

    /// <summary>
    /// Os segundos de validade que o token declara, ou nulo quando ele não os declara de
    /// forma legível.
    /// </summary>
    /// <remarks>
    /// Os instantes vêm de fora e podem estar em qualquer escala — uma expiração gravada em
    /// milissegundos, por exemplo, estoura a faixa de datas representáveis. Isso é mais um
    /// caso de validade ilegível, e não pode impedir o token de chegar à origem: quem o
    /// valida é ela.
    /// </remarks>
    private long? LerValidadeDeclarada(string token)
    {
        if (LerInstantes(token) is not { } instantes || !instantes.EstaNaFaixaDeDatas())
        {
            return null;
        }

        return instantes.EmitidoEm is { } emissao
            ? instantes.ExpiraEm - emissao
            : (long)(DateTimeOffset.FromUnixTimeSeconds(instantes.ExpiraEm) - _relogio.GetUtcNow()).TotalSeconds;
    }

    /// <summary>
    /// Lê os instantes de expiração e emissão declarados no token, sem verificar a
    /// assinatura — a verificação é da origem, e o que se busca aqui é só o momento de
    /// renovar.
    /// </summary>
    private static InstantesDoToken? LerInstantes(string token) =>
        token.Split('.') is [_, { Length: > 0 } corpo, ..] ? DecodificarInstantes(corpo) : null;

    /// <summary>
    /// Decodifica o trecho do meio do token. Apesar do nome, a decodificação lança quando
    /// encontra caractere fora do alfabeto — o valor de retorno cobre só o caso de destino
    /// pequeno demais. Sem esta captura, um token que não seja um dos três trechos
    /// separados por ponto derrubaria a autenticação inteira, em vez de apenas deixar a
    /// validade indisponível.
    /// </summary>
    private static InstantesDoToken? DecodificarInstantes(string corpo)
    {
        try
        {
            return ExtrairInstantes(corpo);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static InstantesDoToken? ExtrairInstantes(string corpo)
    {
        byte[] bytes = new byte[Base64Url.GetMaxDecodedLength(corpo.Length)];

        if (!Base64Url.TryDecodeFromChars(corpo, bytes, out int escritos))
        {
            return null;
        }

        using JsonDocument documento = JsonDocument.Parse(bytes.AsMemory(0, escritos));
        JsonElement raiz = documento.RootElement;

        return raiz.TryGetProperty("exp", out JsonElement expiracao) && expiracao.TryGetInt64(out long expiraEm)
            ? new InstantesDoToken(expiraEm, LerEmissao(raiz))
            : null;
    }

    private static long? LerEmissao(JsonElement raiz) =>
        raiz.TryGetProperty("iat", out JsonElement emissao) && emissao.TryGetInt64(out long emitidoEm)
            ? emitidoEm
            : null;

    public void Dispose()
    {
        if (_descartado)
        {
            return;
        }

        _acessoExclusivo.Dispose();
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

    /// <summary>
    /// Os instantes que o token declara, em segundos desde a época; a emissão pode faltar.
    /// </summary>
    private readonly record struct InstantesDoToken(long ExpiraEm, long? EmitidoEm)
    {
        /// <summary>
        /// Diz se os instantes declarados cabem na faixa de datas que se consegue
        /// representar.
        /// </summary>
        public bool EstaNaFaixaDeDatas() =>
            CabeNaFaixa(ExpiraEm) && (EmitidoEm is not { } emissao || CabeNaFaixa(emissao));

        private static bool CabeNaFaixa(long segundosDesdeAEpoca) =>
            segundosDesdeAEpoca >= DateTimeOffset.MinValue.ToUnixTimeSeconds()
            && segundosDesdeAEpoca <= DateTimeOffset.MaxValue.ToUnixTimeSeconds();
    }
}
