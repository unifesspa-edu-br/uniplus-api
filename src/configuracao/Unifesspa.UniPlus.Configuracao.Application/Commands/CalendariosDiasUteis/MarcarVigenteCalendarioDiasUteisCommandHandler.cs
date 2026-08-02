namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="MarcarVigenteCalendarioDiasUteisCommand"/>: localiza o
/// vigente anterior (se houver) e o desmarca antes de marcar o novo — a invariante
/// "no máximo um vigente" é aplicada aqui (cross-agregado), reforçada pelo índice
/// único parcial de banco como defesa de última linha.
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

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
