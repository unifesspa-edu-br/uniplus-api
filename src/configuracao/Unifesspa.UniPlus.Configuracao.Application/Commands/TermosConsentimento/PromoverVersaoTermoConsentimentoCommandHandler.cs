namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TermosConsentimento;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;
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
        // (recarregado do banco); ver TermoConsentimento.Promover. Também força um
        // UPDATE do termo amarrado ao xmin lido na consulta.
        await repository.AdicionarVersaoAsync(termo, promoverResult.Value!, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Corrida com uma edição concorrente do MESMO rascunho (ex.: outra
            // requisição alterou texto/base legal entre a leitura e este commit,
            // revertendo a revisão) — sem o xmin, a versão sairia gravada a partir
            // de conteúdo já invalidado. O filtro do `when` garante que outras
            // exceções propagam intactas.
            //
            // Descarta o rastreamento da versão Added e do termo Modified ANTES de
            // devolver a falha: o outbox do Wolverine chama SaveChangesAsync de novo
            // depois que este handler retorna (ADR-0004), e sem isso a mesma
            // DbUpdateConcurrencyException estouraria de novo fora deste catch,
            // virando 500 em vez do 409 que acabamos de traduzir.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                TermoConsentimentoErrorCodes.ConflitoDeConcorrencia,
                "O rascunho foi modificado concorrentemente. Revise e promova novamente."));
        }

        return Result.Success();
    }
}
