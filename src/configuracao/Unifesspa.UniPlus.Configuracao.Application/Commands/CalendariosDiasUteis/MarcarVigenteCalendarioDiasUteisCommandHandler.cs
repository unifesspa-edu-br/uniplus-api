namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="MarcarVigenteCalendarioDiasUteisCommand"/>: localiza o
/// vigente anterior (se houver) e o desmarca antes de marcar o novo — a invariante
/// "no máximo um vigente" é aplicada aqui (cross-agregado), reforçada pela
/// exclusion constraint de banco como defesa de última linha.
/// </summary>
public static class MarcarVigenteCalendarioDiasUteisCommandHandler
{
    public static async Task<Result> Handle(
        MarcarVigenteCalendarioDiasUteisCommand command,
        ICalendarioDiasUteisRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        CalendarioDiasUteis? calendario = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (calendario is null)
        {
            return Result.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.NaoEncontrado,
                "Calendário de dias úteis não encontrado."));
        }

        Result marcarResult = calendario.MarcarVigente();
        if (marcarResult.IsFailure)
        {
            return marcarResult;
        }

        CalendarioDiasUteis? vigenteAnterior = await repository
            .ObterVigenteAsync(cancellationToken)
            .ConfigureAwait(false);
        if (vigenteAnterior is not null && vigenteAnterior.Id != calendario.Id)
        {
            vigenteAnterior.MarcarNaoVigente();
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

            // A exclusion constraint de vigência é DEFERRABLE INITIALLY DEFERRED — sem
            // forçar a checagem aqui, ela só rodaria no commit externo que o outbox do
            // Wolverine executa DEPOIS deste handler retornar (UseEntityFrameworkCoreTransactions
            // + AutoApplyTransactions, ADR-0004), fora do alcance deste catch.
            await unitOfWork.ForcarChecagemImediataDeConstraintsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExclusionConstraintViolation.IsVigenteConflict(ex))
        {
            // Corrida entre duas ativações concorrentes: exclusion constraint,
            // dois registros disputando o mesmo "vigente = true". Diferente de
            // DbUpdateConcurrencyException (xmin, tratado no catch abaixo), essa
            // violação chega DEFERRED, no COMMIT que o outbox do Wolverine
            // executa fora deste handler (ADR-0004) — mecanismo não coberto pela
            // investigação empírica da ADR-0119, então este branch mantém o
            // catch local sem `ChangeTracker.Clear()` como débito pré-existente
            // rastreado à parte (a checagem imediata acima só antecipa QUANDO a
            // violação aparece, não muda o mecanismo em si).
            return Result.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia,
                "Outra alteração concorrente já marcou um dataset como vigente. Tente novamente."));
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            // Corrida entre esta ativação e uma remoção concorrente do MESMO
            // dataset (xmin) — padrão canônico da ADR-0119: como o endpoint tem
            // [RequiresIdempotencyKey], a exceção não pode propagar sem catch
            // (IdempotencyFilter não verifica ResourceExecutedContext.Exception
            // antes de cachear a resposta — issue #1028). Descarta o
            // rastreamento ANTES de devolver: o SaveChangesAsync automático do
            // outbox roda de novo depois que este handler retorna, e sem isso a
            // mesma exceção estouraria de novo fora deste catch, virando 500 em
            // vez de 409.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.ConflitoDeConcorrencia,
                "Este dataset foi modificado concorrentemente (possivelmente removido). Tente novamente."));
        }

        return Result.Success();
    }
}
