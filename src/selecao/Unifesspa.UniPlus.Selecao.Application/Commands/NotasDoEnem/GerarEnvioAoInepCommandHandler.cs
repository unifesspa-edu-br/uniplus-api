namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using Domain.Interfaces;
using Domain.Leiautes;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Microsoft.Extensions.Logging;

using Services.NotasDoEnem;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confere o leiaute, bloqueia o processo e confirma a evidência do envio ao INEP.</summary>
public static partial class GerarEnvioAoInepCommandHandler
{
    public static async Task<Result<EnvioAoInepGeradoDto>> Handle(GerarEnvioAoInepCommand command,
        GeracaoDoEnvioAoInep geracao, IProcessoSeletivoRepository processos, IUnitOfWork unitOfWork,
        TimeProvider relogio, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(geracao);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(logger);
        Result<ILeiauteDeArquivoDoInep> leiaute = ResolvedorDoEscopoDeNotasDoEnem.ObterLeiaute(command.LeiauteCodigo, command.LeiauteVersao, TipoDeLeiauteDoInep.Envio);
        Result<EnvioAoInepGeradoDto> resultado = leiaute.IsFailure ? Result<EnvioAoInepGeradoDto>.Failure(leiaute.Error!)
            : await GerarSobBloqueioAsync(command, leiaute.Value!, geracao, processos, relogio, cancellationToken).ConfigureAwait(false);
        return await ConfirmarAsync(resultado, command.ProcessoSeletivoId, unitOfWork, logger, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<EnvioAoInepGeradoDto>> GerarSobBloqueioAsync(GerarEnvioAoInepCommand command,
        ILeiauteDeArquivoDoInep leiaute, GeracaoDoEnvioAoInep geracao, IProcessoSeletivoRepository processos,
        TimeProvider relogio, CancellationToken cancellationToken)
    {
        await processos.TravarAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return await geracao.GerarAsync(command, leiaute, relogio.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<EnvioAoInepGeradoDto>> ConfirmarAsync(Result<EnvioAoInepGeradoDto> resultado,
        Guid processoId, IUnitOfWork unitOfWork, ILogger logger, CancellationToken cancellationToken)
    {
        if (resultado.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            LogEnvioGerado(logger, resultado.Value!.GeracaoId, processoId, resultado.Value.TotalDeCpfs);
        }
        return resultado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Envio ao INEP {GeracaoId} gerado para o processo {ProcessoId} com {TotalDeCpfs} CPFs")]
    private static partial void LogEnvioGerado(ILogger logger, Guid geracaoId, Guid processoId, int totalDeCpfs);
}
