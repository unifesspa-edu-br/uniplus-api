namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class DesativarBaseLegalBonusRegionalCommandHandler
{
    public static async Task<Result> Handle(
        DesativarBaseLegalBonusRegionalCommand command,
        IBaseLegalBonusRegionalRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        BaseLegalBonusRegional? entity = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure(new DomainError(BaseLegalBonusRegionalErrorCodes.NaoEncontrado, "Base legal de bônus regional não encontrada."));
        }

        repository.Remover(entity);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
