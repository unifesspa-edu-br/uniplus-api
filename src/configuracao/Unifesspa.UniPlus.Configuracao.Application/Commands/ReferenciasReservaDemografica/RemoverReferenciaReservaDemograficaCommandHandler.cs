namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="RemoverReferenciaReservaDemograficaCommand"/>.
/// Soft-delete via <c>SoftDeleteInterceptor</c>. Cadastro flat: não há vínculo
/// intra-banco que bloqueie a remoção, e referências congeladas por valor em
/// outro banco (ADR-0061) são desacopladas e nunca bloqueiam (CA-05).
/// </summary>
public static class RemoverReferenciaReservaDemograficaCommandHandler
{
    public static async Task<Result> Handle(
        RemoverReferenciaReservaDemograficaCommand command,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ReferenciaReservaDemografica? referencia = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (referencia is null)
        {
            return Result.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
                "Referência de reserva demográfica não encontrada."));
        }

        repository.Remover(referencia);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
