namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ReativarTipoProcessoCommandHandler
{
    public static async Task<Result> Handle(
        ReativarTipoProcessoCommand command,
        ITipoProcessoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TipoProcesso? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(TipoProcessoErrorCodes.NaoEncontrado, "Tipo de processo seletivo não encontrado."));
        }

        Result reativar = tipo.Reativar();
        if (reativar.IsFailure)
        {
            return reativar;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Descarta antes de retornar: o SaveChanges do outbox rodaria depois e estouraria fora deste catch (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TipoProcessoErrorCodes.ConflitoDeConcorrencia,
                "Este tipo de processo seletivo foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}
