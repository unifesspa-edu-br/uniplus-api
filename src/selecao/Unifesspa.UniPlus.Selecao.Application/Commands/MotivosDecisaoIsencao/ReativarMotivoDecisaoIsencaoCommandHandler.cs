namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da reativação de motivo.</summary>
public static class ReativarMotivoDecisaoIsencaoCommandHandler
{
    public static async Task<Result> Handle(
        ReativarMotivoDecisaoIsencaoCommand command,
        IMotivoDecisaoIsencaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        MotivoDecisaoIsencao? motivo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (motivo is null)
        {
            return Result.Failure(MotivoDecisaoIsencaoHandlerErros.NaoEncontrado(command.Id));
        }

        Result reativacao = motivo.Reativar();
        if (reativacao.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return reativacao;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // As duas requisições leram o mesmo estado e atravessaram a guarda do agregado. Sem o
            // descarte, o SaveChanges que o Wolverine dispara depois do handler repetiria o UPDATE.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(MotivoDecisaoIsencaoHandlerErros.SituacaoAlteradaConcorrentemente());
        }

        return Result.Success();
    }
}
