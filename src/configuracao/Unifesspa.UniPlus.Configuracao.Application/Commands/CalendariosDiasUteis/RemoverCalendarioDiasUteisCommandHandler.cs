namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverCalendarioDiasUteisCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>; os dias não úteis (filhos, sem soft-delete
/// próprio) permanecem intactos sob a linha soft-deleted (ADR do padrão
/// Unidade/Historico). Recusa remover o dataset vigente — trocar de vigente é
/// operação explícita (<c>MarcarVigenteCalendarioDiasUteisCommand</c>), nunca
/// efeito colateral de uma remoção.
/// </summary>
public static class RemoverCalendarioDiasUteisCommandHandler
{
    public static async Task<Result> Handle(
        RemoverCalendarioDiasUteisCommand command,
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

        if (calendario.Vigente)
        {
            return Result.Failure(new DomainError(
                CalendarioDiasUteisErrorCodes.NaoRemoveVigente,
                "O dataset vigente não pode ser removido — marque outro dataset como vigente antes."));
        }

        repository.Remover(calendario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
