namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
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

        // O xmin de TermoConsentimento (ver TermoConsentimentoConfiguration) guarda
        // qualquer UPDATE concorrente — o soft-delete é um UPDATE
        // (IsDeleted/DeletedAt/DeletedBy), então colide com uma edição concorrente
        // do mesmo termo. DbUpdateConcurrencyException propaga sem catch local: o
        // GlobalExceptionMiddleware mapeia para 409 (ADR-0119) — deixar propagar
        // evita que o SaveChangesAsync automático do outbox (ADR-0004) tente rodar
        // de novo sobre entidades ainda sujas.
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
