namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Handler do <see cref="DefinirOpcoesDeCursoCommand"/>: a faixa do número e a conferência contra
/// as ofertas da distribuição são do agregado (<see cref="ProcessoSeletivo.DefinirOpcoesDeCurso"/>).
/// </summary>
public static class DefinirOpcoesDeCursoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirOpcoesDeCursoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(command, obtido.Value!, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirEGravarAsync(
        DefinirOpcoesDeCursoCommand command,
        ProcessoSeletivo processo,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = processo.DefinirOpcoesDeCurso(command.Numero);
        return definido.IsFailure
            ? RecusaDaDefinicao(definido)
            : await GravarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Só a recusa de regra tem campo; o que vem sem campo é do agregado (precondição).
    /// </summary>
    private static Result RecusaDaDefinicao(Result definido) =>
        definido.Errors[0].Field is null
            ? Result.Failure(definido.Error!)
            : Result.ValidationFailure(definido.Errors);

    /// <summary>
    /// O endpoint tem Idempotency-Key (ADR-0119): a concorrência otimista é capturada aqui e o rastreamento é
    /// descartado, para o <c>SaveChangesAsync</c> do outbox não reencontrar o agregado modificado.
    /// </summary>
    private static async Task<Result> GravarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia,
                "O processo foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}
