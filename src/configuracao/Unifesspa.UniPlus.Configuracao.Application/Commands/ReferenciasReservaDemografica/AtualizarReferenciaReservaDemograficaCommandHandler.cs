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
            : await AtualizarAsync(command, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AtualizarAsync(
        AtualizarReferenciaReservaDemograficaCommand command,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ReferenciaReservaDemografica? referencia = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        return referencia is null
            ? Result.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
                "Referência de reserva demográfica não encontrada."))
            : await AplicarAsync(command, referencia, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarAsync(
        AtualizarReferenciaReservaDemograficaCommand command,
        ReferenciaReservaDemografica referencia,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (await CensoMudouParaUmJaExistenteAsync(command, referencia, repository, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CensoJaExisteErro());
        }

        Result atualizada = referencia.Atualizar(
            command.CensoReferencia, command.PpiPercentual, command.QuilombolaPercentual, command.PcdPercentual, command.BaseLegal);

        return atualizada.IsFailure
            ? atualizada
            : await SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    /// <remarks>
    /// O Censo é mutável de propósito (decisão do P.O., #593): o Processo Seletivo congela o Censo e os percentuais por
    /// valor no snapshot de publicação (ADR-0061), então editar a fonte viva não retroage sobre nada já publicado.
    /// Diferente do <c>codigo</c> da Modalidade (#589), que é imutável por participar do hash de publicação (UNI-REQ-0019).
    /// Como o Censo é a chave de negócio única entre vivos, a colisão só é conferida quando ele muda. A comparação é
    /// ordinal, para casar com o repositório e com o índice único parcial: ambos comparam o valor bruto.
    /// </remarks>
    private static async Task<bool> CensoMudouParaUmJaExistenteAsync(
        AtualizarReferenciaReservaDemograficaCommand command,
        ReferenciaReservaDemografica referencia,
        IReferenciaReservaDemograficaRepository repository,
        CancellationToken cancellationToken) =>
        command.CensoReferencia is { } censo
        && !string.Equals(censo.Trim(), referencia.CensoReferencia, StringComparison.Ordinal)
        && await repository.CensoExisteEntreLivosAsync(censo, command.Id, cancellationToken).ConfigureAwait(false);

    /// <remarks>
    /// Corrida entre a conferência do Censo e o UPDATE: o índice único parcial dispara 23505 e o resultado é o mesmo
    /// <c>CensoJaExiste</c> do caminho sem corrida (409 consistente em vez de 500). O filtro <c>when</c> deixa as
    /// demais exceções propagarem.
    /// </remarks>
    private static async Task<Result> SalvarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCensoConflict(constraint))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            return Result.Failure(CensoJaExisteErro());
        }
    }

    private static DomainError CensoJaExisteErro() =>
        new(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
            "Já existe uma Referência de reserva demográfica viva para o Censo informado.");
}
