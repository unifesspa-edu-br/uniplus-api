namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="DesativarObrigatoriedadeLegalCommand"/>.
/// Marca a regra para soft-delete via <c>SoftDeleteInterceptor</c> — o EF
/// converte <c>EntityState.Deleted</c> em <c>Modified + IsDeleted = true</c>,
/// e o <c>ObrigatoriedadeLegalHistoricoInterceptor</c> grava o snapshot da
/// desativação (ADR-0058 Emenda 1 + ADR-0063).
/// </summary>
public static class DesativarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result> Handle(
        DesativarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.NaoEncontrada",
                $"ObrigatoriedadeLegal {command.Id} não encontrada."));
        }

        repository.Remover(regra);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
