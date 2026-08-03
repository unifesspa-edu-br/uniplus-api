namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class RemoverTermoConsentimentoCommandHandler
{
    public static async Task<Result> Handle(
        RemoverTermoConsentimentoCommand command,
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

        if (termo.Versoes.Count > 0)
        {
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.RemocaoBloqueadaComVersaoPromovida,
                "Termo com ao menos uma versão promovida não pode ser removido — edite o rascunho e promova uma nova versão."));
        }

        repository.Remover(termo);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // O xmin de TermoConsentimento (ver TermoConsentimentoConfiguration)
            // guarda qualquer UPDATE concorrente — o soft-delete é um UPDATE
            // (IsDeleted/DeletedAt/DeletedBy), então colide com uma edição
            // concorrente do mesmo termo. Descarta o rastreamento ANTES de
            // devolver: o outbox do Wolverine chama SaveChangesAsync de novo após o
            // handler retornar (ADR-0004), e sem isso a mesma exceção estouraria
            // fora deste catch, virando 500 em vez de 409.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.ConflitoDeConcorrencia,
                "O termo foi modificado concorrentemente. Recarregue e tente novamente."));
        }

        return Result.Success();
    }
}
