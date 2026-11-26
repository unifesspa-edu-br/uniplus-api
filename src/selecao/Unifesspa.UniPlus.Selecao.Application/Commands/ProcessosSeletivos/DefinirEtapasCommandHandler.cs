namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public static class DefinirEtapasCommandHandler
{
    /// <summary>
    /// Substitui as etapas do processo, reconciliando por <c>Id</c> (ver <see cref="DefinicaoDasEtapas"/>).
    /// </summary>
    /// <remarks>
    /// SEM <c>[NonTransactional]</c>, de propósito: este handler depende da transação ambiente do Wolverine
    /// para <see cref="IProcessoSeletivoRepository.ObterParaMutacaoAsync"/>. O <c>SELECT ... FOR UPDATE</c>
    /// só serializa handlers concorrentes porque roda enrolado nela, e <c>[NonTransactional]</c> o
    /// desabilitaria.
    /// </remarks>
    public static async Task<Result> Handle(
        DefinirEtapasCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        DefinicaoDasEtapas definicao,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, definicao, unitOfWork, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        DefinicaoDasEtapas definicao,
        IUnitOfWork unitOfWork,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result definida = await definicao.DefinirAsync(processo, command, agora, cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
