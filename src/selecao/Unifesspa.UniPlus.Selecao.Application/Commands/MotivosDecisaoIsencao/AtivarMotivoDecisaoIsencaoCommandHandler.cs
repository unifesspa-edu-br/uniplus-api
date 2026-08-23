namespace Unifesspa.UniPlus.Selecao.Application.Commands.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da reativação de motivo.</summary>
public static class AtivarMotivoDecisaoIsencaoCommandHandler
{
    public static async Task<Result> Handle(
        AtivarMotivoDecisaoIsencaoCommand command,
        IMotivoDecisaoIsencaoRepository repository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        MotivoDecisaoIsencao? motivo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);

        if (motivo is null)
        {
            return Result.Failure(MotivoDecisaoIsencaoHandlerErros.NaoEncontrado(command.Id));
        }

        Result ativar = motivo.Ativar();
        if (ativar.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return ativar;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Outra requisição mudou a situação entre a leitura e a gravação. A
            // guarda do agregado não alcança essa corrida: as duas leram o mesmo
            // estado e as duas a atravessaram. Sem o descarte, o SaveChangesAsync
            // que o Wolverine dispara depois do handler tentaria o mesmo UPDATE
            // fora deste catch.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(MotivoDecisaoIsencaoHandlerErros.SituacaoAlteradaConcorrentemente());
        }

        return Result.Success();
    }
}
