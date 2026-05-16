namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based do <see cref="DesativarObrigatoriedadeLegalCommand"/>.
/// Marca a regra para soft-delete via <c>SoftDeleteInterceptor</c> — o EF
/// converte <c>EntityState.Deleted</c> em <c>Modified + IsDeleted = true</c>,
/// e o <c>ObrigatoriedadeLegalHistoricoInterceptor</c> grava o snapshot da
/// desativação (ADR-0058 Emenda 1 + ADR-0063). Junction temporal é
/// preservada — os bindings continuam ativos até que o admin emita um PUT
/// reciclando a regra com novo Hash.
/// </summary>
public static class DesativarObrigatoriedadeLegalCommandHandler
{
    public static async Task<Result> Handle(
        DesativarObrigatoriedadeLegalCommand command,
        IObrigatoriedadeLegalRepository repository,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (regra is null)
        {
            return Result.Failure(new DomainError(
                "ObrigatoriedadeLegal.NaoEncontrada",
                $"ObrigatoriedadeLegal {command.Id} não encontrada."));
        }

        Result authz = AreaScopedAuthorization.Autorizar(userContext, regra.Proprietario);
        if (authz.IsFailure)
        {
            return authz;
        }

        repository.Remover(regra);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
