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

        Result ativar = tipo.Ativar();
        if (ativar.IsFailure)
        {
            return ativar;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Duas reativações concorrentes do mesmo tipo leem `Ativo == false` e
            // ambas atravessam a guarda do agregado — a corrida só é vista pelo
            // xmin, na gravação. Como o endpoint tem [RequiresIdempotencyKey], a
            // exceção não pode propagar sem catch (ADR-0119: o IdempotencyFilter
            // não confere ResourceExecutedContext.Exception antes de cachear a
            // resposta). O descarte vem antes do retorno porque o
            // SaveChangesAsync automático do outbox roda depois que o handler
            // retorna e reencontraria a mesma entidade modificada, estourando de
            // novo fora deste catch — 500 em vez de 409.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TipoProcessoErrorCodes.ConflitoDeConcorrencia,
                "Este tipo de processo seletivo foi alterado concorrentemente. Tente novamente."));
        }

        return Result.Success();
    }
}
