namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="EditarRascunhoTermoConsentimentoCommand"/>. Valida o
/// payload (422, sem I/O) ANTES de buscar o termo por Id — validação sempre
/// vence 404.
/// </summary>
public static class EditarRascunhoTermoConsentimentoCommandHandler
{
    public static async Task<Result> Handle(
        EditarRascunhoTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result conferenciaDaDeclaracao = TermoConsentimento.ValidarCamposDoPayload(
            command.TextoRascunho, command.BaseLegalRascunho, command.FormaAceiteRascunho);

        return conferenciaDaDeclaracao.IsFailure
            ? Result.ValidationFailure(conferenciaDaDeclaracao.Errors)
            : await EditarAsync(command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> EditarAsync(
        EditarRascunhoTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        TermoConsentimento? termo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (termo is null)
        {
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.NaoEncontrado,
                "Termo de consentimento não encontrado."));
        }

        Result editado = termo.EditarRascunho(command.TextoRascunho, command.BaseLegalRascunho, command.FormaAceiteRascunho);

        return editado.IsFailure
            ? editado
            : await SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// O xmin de TermoConsentimento (ver TermoConsentimentoConfiguration) guarda qualquer UPDATE concorrente, não só a
    /// promoção: duas edições simultâneas do mesmo rascunho colidem aqui. O rastreamento é descartado ANTES de devolver
    /// porque o outbox do Wolverine chama SaveChangesAsync de novo após o handler retornar (ADR-0004); sem isso a mesma
    /// exceção estouraria fora deste catch, virando 500 em vez de 409.
    /// </remarks>
    private static async Task<Result> SalvarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.ConflitoDeConcorrencia,
                "O termo foi modificado concorrentemente. Recarregue e tente novamente."));
        }
    }
}
