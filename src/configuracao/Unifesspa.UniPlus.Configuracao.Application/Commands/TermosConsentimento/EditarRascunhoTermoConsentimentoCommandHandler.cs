namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class EditarRascunhoTermoConsentimentoCommandHandler
{
    public static async Task<Result> Handle(
        EditarRascunhoTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        TermoConsentimento? termo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (termo is null)
        {
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.NaoEncontrado,
                "Termo de consentimento não encontrado."));
        }

        Result editarResult = termo.EditarRascunho(command.TextoRascunho, command.BaseLegalRascunho, command.FormaAceiteRascunho);
        if (editarResult.IsFailure)
        {
            return editarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // O xmin de TermoConsentimento (ver TermoConsentimentoConfiguration)
            // guarda qualquer UPDATE concorrente, não só a promoção — duas edições
            // simultâneas do mesmo rascunho colidem aqui. Descarta o rastreamento
            // ANTES de devolver: o outbox do Wolverine chama SaveChangesAsync de
            // novo após o handler retornar (ADR-0004), e sem isso a mesma exceção
            // estouraria fora deste catch, virando 500 em vez de 409.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.ConflitoDeConcorrencia,
                "O termo foi modificado concorrentemente. Recarregue e tente novamente."));
        }

        return Result.Success();
    }
}
