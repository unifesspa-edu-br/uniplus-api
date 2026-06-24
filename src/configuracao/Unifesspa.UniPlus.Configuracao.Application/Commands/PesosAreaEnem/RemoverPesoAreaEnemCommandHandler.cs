namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverPesoAreaEnemCommand"/>. Soft-delete via
/// <c>SoftDeleteInterceptor</c>. Nenhuma FK aponta para este cadastro: a ligação
/// do curso é por valor sobre o vocabulário de grupos e o congelamento em outro
/// banco (ADR-0061) é cópia desacoplada — por isso a remoção nunca é bloqueada (CA-05).
/// </summary>
public static class RemoverPesoAreaEnemCommandHandler
{
    public static async Task<Result> Handle(
        RemoverPesoAreaEnemCommand command,
        IPesoAreaEnemRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        PesoAreaEnem? peso = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (peso is null)
        {
            return Result.Failure(new DomainError(
                PesoAreaEnemErrorCodes.NaoEncontrado,
                "Linha de pesos do ENEM não encontrada."));
        }

        repository.Remover(peso);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
