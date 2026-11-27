namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Services;

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
        LeituraDoInstanteDoComando instantes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(instantes);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, definicao, unitOfWork, instantes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        DefinicaoDasEtapas definicao,
        IUnitOfWork unitOfWork,
        LeituraDoInstanteDoComando instantes,
        CancellationToken cancellationToken)
    {
        Result<InstanteDoComando> instante = instantes.Ler();
        return instante.IsFailure
            ? Result.Failure(instante.Error!)
            : await GravarAsync(processo, command, definicao, unitOfWork, instante.Value.Dia, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> GravarAsync(
        ProcessoSeletivo processo,
        DefinirEtapasCommand command,
        DefinicaoDasEtapas definicao,
        IUnitOfWork unitOfWork,
        DateOnly hoje,
        CancellationToken cancellationToken)
    {
        Result definida = await definicao.DefinirAsync(processo, command, hoje, cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
