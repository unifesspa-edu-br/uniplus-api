namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarReferenciaReservaDemograficaCommand"/>. Valida
/// o payload (422, sem I/O) ANTES de buscar a referência por Id — validação
/// sempre vence 404.
/// </summary>
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

        Result conferenciaDaDeclaracao = ReferenciaReservaDemografica.ValidarCamposDoPayload(
            command.CensoReferencia, command.PpiPercentual, command.QuilombolaPercentual, command.PcdPercentual, command.BaseLegal);

        return conferenciaDaDeclaracao.IsFailure
            ? Result.ValidationFailure(conferenciaDaDeclaracao.Errors)
            : await AtualizarSeExistirAsync(command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarSeExistirAsync(
        AtualizarReferenciaReservaDemograficaCommand command,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ReferenciaReservaDemografica? referencia = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (referencia is null)
        {
            return Result.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
                "Referência de reserva demográfica não encontrada."));
        }

        Result censo = await CensoUnicoDaReferencia
            .ConferirNaEdicaoAsync(referencia, command.CensoReferencia, repository, cancellationToken).ConfigureAwait(false);

        return censo.IsFailure
            ? censo
            : await AplicarEGravarAsync(referencia, command, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarEGravarAsync(
        ReferenciaReservaDemografica referencia,
        AtualizarReferenciaReservaDemograficaCommand command,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result atualizada = referencia.Atualizar(
            command.CensoReferencia, command.PpiPercentual, command.QuilombolaPercentual, command.PcdPercentual, command.BaseLegal);

        return atualizada.IsFailure
            ? atualizada
            : await CensoUnicoDaReferencia.GravarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
