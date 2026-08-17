namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
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
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result preCheck = ReferenciaReservaDemografica.ValidarCamposDoPayload(
            command.CensoReferencia,
            command.PpiPercentual,
            command.QuilombolaPercentual,
            command.PcdPercentual,
            command.BaseLegal);

        if (preCheck.IsFailure)
        {
            return Result.ValidationFailure(preCheck.Errors);
        }

        ReferenciaReservaDemografica? referencia = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (referencia is null)
        {
            return Result.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
                "Referência de reserva demográfica não encontrada."));
        }

        // O Censo é mutável de propósito (decisão do P.O., #593): não precisa ser
        // imutável porque o Processo Seletivo congela o Censo + percentuais por valor
        // no snapshot de publicação (ADR-0061) — editar a fonte viva não retroage sobre
        // nada já publicado. Diferente do `codigo` da Modalidade (#589), que é imutável
        // por participar do hash de publicação (RN08). Como é a chave de negócio única
        // entre vivos, só checamos colisão quando o Censo muda. A comparação é
        // case-sensitive (Ordinal) para casar com o repositório e o índice único
        // parcial — ambos comparam o valor bruto; não há semântica de maiúsculas no Censo.
        if (!string.Equals(command.CensoReferencia!.Trim(), referencia.CensoReferencia, StringComparison.Ordinal)
            && await repository.CensoExisteEntreLivosAsync(command.CensoReferencia, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CensoJaExisteErro());
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

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCensoConflict(constraint))
        {
            // Corrida entre CensoExisteEntreLivosAsync e o UPDATE: o índice único
            // parcial dispara 23505 e viramos o mesmo CensoJaExiste do caminho
            // não-race — 409 consistente em vez de 500. O `when` deixa outras
            // exceções propagarem.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CensoJaExisteErro());
        }

        return Result.Success();
    }

    private static DomainError CensoJaExisteErro() =>
        new(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
            "Já existe uma Referência de reserva demográfica viva para o Censo informado.");
}
