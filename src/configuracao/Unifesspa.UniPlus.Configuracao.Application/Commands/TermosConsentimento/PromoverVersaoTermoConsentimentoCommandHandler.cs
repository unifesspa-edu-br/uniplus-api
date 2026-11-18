namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class PromoverVersaoTermoConsentimentoCommandHandler
{
    public static async Task<Result> Handle(
        PromoverVersaoTermoConsentimentoCommand command,
        ITermoConsentimentoRepository repository,
        IUnitOfWork unitOfWork,
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
            return Result.Failure(new DomainError(TermoConsentimentoErrorCodes.NaoEncontrado, "Termo de consentimento não encontrado."));
        }

        return await PromoverEGravarAsync(termo, repository, unitOfWork, userContext.UserId ?? "system", timeProvider, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<Result> PromoverEGravarAsync(
        TermoConsentimento termo,
        ITermoConsentimentoRepository repository,
        IUnitOfWork unitOfWork,
        string promovidoPor,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Result<TermoConsentimentoVersao> promovida = termo.Promover(promovidoPor, timeProvider.GetUtcNow());
        if (promovida.IsFailure)
        {
            return Result.Failure(promovida.Error!);
        }

        // Adiciona explicitamente ao DbSet: o EF Core não detecta como Added uma entidade só inserida na
        // coleção em memória de um agregado já rastreado. Também força um UPDATE do termo amarrado ao xmin lido.
        await repository.AdicionarVersaoAsync(termo, promovida.Value!, cancellationToken).ConfigureAwait(false);
        return await SalvarTratandoConflitoDeConcorrenciaAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Grava e traduz em conflito a corrida com uma edição concorrente do mesmo rascunho, ou com outra promoção que
    /// leu o mesmo número de versão (índice único de termo e número): sem o xmin, a versão sairia gravada a partir de
    /// conteúdo já invalidado. O descarte do rastreamento vem antes de devolver a falha porque o outbox do Wolverine
    /// chama SaveChangesAsync de novo depois do handler (ADR-0004), e a mesma exceção viraria 500 em vez do 409.
    /// </summary>
    private static async Task<Result> SalvarTratandoConflitoDeConcorrenciaAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex)
            || ConstraintVioladaNaGravacao.NomeDaConstraintViolada(ex) == RestricoesDoBanco.VersaoDoTermoNumeroUnico)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.ConflitoDeConcorrencia,
                "O rascunho foi modificado concorrentemente. Revise e promova novamente."));
        }

        return Result.Success();
    }
}
