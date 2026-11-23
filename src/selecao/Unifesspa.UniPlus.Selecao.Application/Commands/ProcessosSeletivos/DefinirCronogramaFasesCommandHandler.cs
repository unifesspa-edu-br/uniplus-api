namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Define e grava o cronograma, preservando todas as recusas do domínio.
/// </summary>
public static class DefinirCronogramaFasesCommandHandler
{
    public static async Task<Result> Handle(
        DefinirCronogramaFasesCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        DefinicaoDoCronogramaDeFases definicao,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<ProcessoSeletivo> obtido = await ObterParaEdicaoAsync(processoSeletivoRepository, command, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, definicao, unitOfWork, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    // Fora do rascunho a recusa vem antes de resolver regras externas.
    private static async Task<Result<ProcessoSeletivo>> ObterParaEdicaoAsync(
        IProcessoSeletivoRepository repositorio, DefinirCronogramaFasesCommand command, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return processo.MutacaoBloqueada() is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirCronogramaFasesCommand command,
        DefinicaoDoCronogramaDeFases definicao,
        IUnitOfWork unitOfWork,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result definida = await definicao.DefinirAsync(processo, command, agora, cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            return Result.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
