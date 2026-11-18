namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Domain.Entities;
using Domain.Errors;

using Kernel.Results;

using Services;
using Services.Inscricoes;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Cancela a inscrição do titular e confirma a alteração na mesma transação.</summary>
public static class CancelarInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        CancelarInscricaoCommand command, InscricaoDoDono dono, CancelamentoDaInscricaoPeloTitular cancelamento,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(dono);
        ArgumentNullException.ThrowIfNull(cancelamento);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        Result<Inscricao> inscricao = await dono.ObterAtivaAsync(command.ProcessoSeletivoId, "cancelar a inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure ? Result.Failure(inscricao.Error!)
            : await CancelarEConfirmarAsync(inscricao.Value!, command, cancelamento, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> CancelarEConfirmarAsync(
        Inscricao inscricao, CancelarInscricaoCommand command, CancelamentoDaInscricaoPeloTitular cancelamento,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = await cancelamento.CancelarAsync(inscricao, command, cancellationToken).ConfigureAwait(false);
        return resultado.IsFailure ? resultado : await ConfirmarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> ConfirmarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(InscricaoErrorCodes.ConflitoDeConcorrencia, "Esta inscrição foi alterada concorrentemente. Tente novamente."));
        }
    }
}
