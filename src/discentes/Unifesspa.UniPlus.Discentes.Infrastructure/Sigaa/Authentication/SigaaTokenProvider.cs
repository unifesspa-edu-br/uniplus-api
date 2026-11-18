namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
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

    private TokenDeAcesso? _token;
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
    public async Task<TokenDeAcesso> ObterAsync(CancellationToken cancellationToken)
    {
        if (TentarReaproveitar(out TokenDeAcesso? vigente))
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
    public void Descartar(TokenDeAcesso tokenRecusado)
    {
        _acessoExclusivo.Wait();
        try
        {
            if (Equals(_token, tokenRecusado))
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
    private async Task<TokenDeAcesso> ReaproveitarOuRenovarAsync(CancellationToken cancellationToken)
    {
        if (TentarReaproveitar(out TokenDeAcesso? renovadoPorOutra))
        {
            return renovadoPorOutra;
        }

        TokenDeAcesso novo = await AutenticarAsync(cancellationToken).ConfigureAwait(false);
        TimeSpan validade = CalcularValidade(novo);

        Guardar(novo, validade);

        return novo;
    }

    private void Guardar(TokenDeAcesso token, TimeSpan validade)
    {
        _token = token;
        _expiraEm = _relogio.GetUtcNow().Add(validade);

        LogTokenRenovado(_logger, _opcoes.Credenciais.Usuario, (int)validade.TotalSeconds);
    }

    private bool TentarReaproveitar([NotNullWhen(true)] out TokenDeAcesso? token)
    {
        TokenDeAcesso? guardado = _token;

        if (guardado is not null && _relogio.GetUtcNow() < _expiraEm - _opcoes.Token.MargemDeRenovacao)
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
    private async Task<TokenDeAcesso> AutenticarAsync(CancellationToken cancellationToken)
    {
        using HttpClient cliente = _clientes.CreateClient(NomeDoClienteHttp);

        using HttpResponseMessage resposta = await cliente
            .PostAsJsonAsync(
                "authentication_token",
                new PedidoDeAutenticacao(_opcoes.Credenciais.Usuario, _opcoes.Credenciais.Senha),
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
            LogAutenticacaoRecusada(_logger, _opcoes.Credenciais.Usuario, status);

            throw new SigaaAutenticacaoException(
                $"A API do SIGAA recusou a autenticação do usuário de serviço com status {status}.");
        }
    }

    private static async Task<TokenDeAcesso> LerTokenAsync(
        HttpResponseMessage resposta,
        CancellationToken cancellationToken)
    {
        RespostaDeAutenticacao? conteudo = await resposta.Content
            .ReadFromJsonAsync<RespostaDeAutenticacao>(cancellationToken)
            .ConfigureAwait(false);

        return TokenDeAcesso.Criar(conteudo?.Token).Match(
            token => token,
            _ => throw new SigaaAutenticacaoException(
                "A API do SIGAA respondeu à autenticação sem devolver um token."));
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
    private TimeSpan CalcularValidade(TokenDeAcesso token)
    {
        ParametrosDoTokenDoSigaa parametros = _opcoes.Token;
        TimeSpan assumida = Menor(parametros.ValidadeAssumida, parametros.ValidadeMaxima);

        if (LerValidadeDeclarada(token) is not { } declarada)
        {
            LogValidadeIndisponivel(_logger);
            return assumida;
        }

        return declarada > TimeSpan.Zero ? Menor(declarada, parametros.ValidadeMaxima) : assumida;
    }

    private static TimeSpan Menor(TimeSpan primeiro, TimeSpan segundo) => primeiro < segundo ? primeiro : segundo;

    /// <summary>
    /// A validade que o token declara, em segundos inteiros, ou nulo quando ele não a declara
    /// de forma legível.
    /// </summary>
    private TimeSpan? LerValidadeDeclarada(TokenDeAcesso token)
    {
        if (!token.TentarLerInstantesDeclarados(out DateTimeOffset expiraEm, out DateTimeOffset? emitidoEm))
        {
            return null;
        }

        TimeSpan declarada = expiraEm - (emitidoEm ?? _relogio.GetUtcNow());

        return TimeSpan.FromSeconds((long)declarada.TotalSeconds);
    }

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
}
