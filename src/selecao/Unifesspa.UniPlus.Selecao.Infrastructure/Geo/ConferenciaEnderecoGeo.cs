namespace Unifesspa.UniPlus.Selecao.Infrastructure.Geo;

using System.Net;

using Microsoft.Extensions.Logging;

using Polly.CircuitBreaker;
using Polly.Timeout;

using Refit;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Implementação de <see cref="IConferenciaEnderecoGeo"/> contra a API real do Geo (issue #1865).
/// Qualquer falha de rede, timeout ou status 5xx — depois de a política de resiliência já ter
/// esgotado as retentativas —, resposta de sucesso sem corpo utilizável e autenticação impossível
/// (inclusive endpoint OAuth não configurado) são FALHA FECHADA: <see cref="ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel"/>,
/// nunca "aceito".
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI em GeoClientRegistration.")]
internal sealed partial class ConferenciaEnderecoGeo : IConferenciaEnderecoGeo
{
    private readonly IGeoApi _geo;
    private readonly ILogger<ConferenciaEnderecoGeo> _logger;

    public ConferenciaEnderecoGeo(IGeoApi geo, ILogger<ConferenciaEnderecoGeo> logger)
    {
        ArgumentNullException.ThrowIfNull(geo);
        ArgumentNullException.ThrowIfNull(logger);

        _geo = geo;
        _logger = logger;
    }

    public async Task<Result> ConferirCepAsync(
        string cep, string cidadeCodigoIbgeDeclarada, string ufDeclarada, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cep);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidadeCodigoIbgeDeclarada);
        ArgumentException.ThrowIfNullOrWhiteSpace(ufDeclarada);

        ApiResponse<CepResolvidoResponse> resposta;
        try
        {
            resposta = await _geo.ResolverCepAsync(cep, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or TimeoutException
            or TimeoutRejectedException or BrokenCircuitException or GeoAutenticacaoException)
        {
            LogGeoIndisponivel(_logger, "cep", excecao);
            return Result.Failure(ErroIndisponivel());
        }

        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, "O CEP informado não tem cobertura no Geo."));
        }

        if (!resposta.IsSuccessStatusCode)
        {
            LogRespostaInesperada(_logger, "cep", (int)(resposta.StatusCode ?? default));
            return Result.Failure(ErroIndisponivel());
        }

        if (resposta.Content is not { } corpo)
        {
            LogRespostaSemCorpo(_logger, "cep", (int)resposta.StatusCode);
            return Result.Failure(ErroIndisponivel());
        }

        bool bate = string.Equals(corpo.CodigoIbge, cidadeCodigoIbgeDeclarada, StringComparison.Ordinal)
            && string.Equals(corpo.Uf, ufDeclarada, StringComparison.OrdinalIgnoreCase);

        return bate
            ? Result.Success()
            : Result.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.CidadeDivergente,
                "O CEP informado resolve para uma cidade diferente da declarada."));
    }

    public async Task<Result> ConferirMunicipioAsync(
        string cidadeCodigoIbgeDeclarado, string ufDeclarada, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cidadeCodigoIbgeDeclarado);
        ArgumentException.ThrowIfNullOrWhiteSpace(ufDeclarada);

        ApiResponse<CidadeDetalheResponse> resposta;
        try
        {
            resposta = await _geo.ObterCidadeAsync(cidadeCodigoIbgeDeclarado, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException or TimeoutException
            or TimeoutRejectedException or BrokenCircuitException or GeoAutenticacaoException)
        {
            LogGeoIndisponivel(_logger, "cidade", excecao);
            return Result.Failure(ErroIndisponivel());
        }

        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, "O município informado não existe no Geo."));
        }

        if (!resposta.IsSuccessStatusCode)
        {
            LogRespostaInesperada(_logger, "cidade", (int)(resposta.StatusCode ?? default));
            return Result.Failure(ErroIndisponivel());
        }

        if (resposta.Content is not { } corpo)
        {
            LogRespostaSemCorpo(_logger, "cidade", (int)resposta.StatusCode);
            return Result.Failure(ErroIndisponivel());
        }

        return string.Equals(corpo.Uf, ufDeclarada, StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : Result.Failure(new DomainError(
                ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente,
                "O município informado não pertence à UF declarada."));
    }

    private static DomainError ErroIndisponivel() => new(
        ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel,
        "O Geo não respondeu — a submissão é recusada, mas o rascunho continua salvável.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "O Geo não respondeu à conferência de {Recurso}.")]
    private static partial void LogGeoIndisponivel(ILogger logger, string recurso, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O Geo respondeu à conferência de {Recurso} com sucesso ({Status}), mas sem corpo utilizável.")]
    private static partial void LogRespostaSemCorpo(ILogger logger, string recurso, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "O Geo respondeu à conferência de {Recurso} com status inesperado {Status}.")]
    private static partial void LogRespostaInesperada(ILogger logger, string recurso, int status);
}
