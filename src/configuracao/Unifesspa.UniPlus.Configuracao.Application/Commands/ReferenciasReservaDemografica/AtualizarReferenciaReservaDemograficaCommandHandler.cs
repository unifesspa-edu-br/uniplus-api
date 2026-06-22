namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarReferenciaReservaDemograficaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarReferenciaReservaDemograficaCommand command,
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

        // O Censo é a chave de negócio única entre vivos — só checa colisão quando muda.
        if (!string.Equals(command.CensoReferencia.Trim(), referencia.CensoReferencia, StringComparison.OrdinalIgnoreCase)
            && await repository.CensoExisteEntreLivosAsync(command.CensoReferencia, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
                $"Já existe uma Referência de reserva demográfica viva para o Censo '{command.CensoReferencia}'."));
        }

        Result atualizarResult = referencia.Atualizar(
            command.CensoReferencia,
            command.PpiPercentual,
            command.QuilombolaPercentual,
            command.PcdPercentual,
            command.BaseLegal);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
