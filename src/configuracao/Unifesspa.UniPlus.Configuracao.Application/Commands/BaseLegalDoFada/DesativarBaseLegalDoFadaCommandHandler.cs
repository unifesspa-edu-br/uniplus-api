namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class DesativarBaseLegalDoFadaCommandHandler
{
    public static async Task<Result> Handle(
        DesativarBaseLegalDoFadaCommand command,
        IBaseLegalDoFadaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        BaseLegalDoFada? entity = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return Result.Failure(new DomainError(BaseLegalDoFadaErrorCodes.NaoEncontrado, "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada."));
        }

        repository.Remover(entity);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
