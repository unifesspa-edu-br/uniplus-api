namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class PromoverVersaoTermoConsentimentoCommandHandler
{
    public static async Task<Result> Handle(
        PromoverVersaoTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        TermoConsentimento? termo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (termo is null)
        {
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.NaoEncontrado,
                "Termo de consentimento não encontrado."));
        }

        string promovidoPor = userContext.UserId ?? "system";
        DateTimeOffset agora = timeProvider.GetUtcNow();

        Result<TermoConsentimentoVersao> promoverResult = termo.Promover(promovidoPor, agora);
        if (promoverResult.IsFailure)
        {
            return Result.Failure(promoverResult.Error!);
        }

        // Adiciona explicitamente ao DbSet — o EF Core não detecta como Added uma
        // entidade só inserida na coleção em memória de um agregado já rastreado
        // (recarregado do banco); ver TermoConsentimento.Promover.
        await repository.AdicionarVersaoAsync(promoverResult.Value!, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
