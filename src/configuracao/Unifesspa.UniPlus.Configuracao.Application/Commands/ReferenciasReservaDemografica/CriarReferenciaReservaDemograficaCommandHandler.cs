namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarReferenciaReservaDemograficaCommand"/>
/// (convention-based Wolverine): confere a unicidade do Censo entre referências
/// vivas, cria o agregado, persiste e commita.
/// </summary>
public static class CriarReferenciaReservaDemograficaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarReferenciaReservaDemograficaCommand command,
        IReferenciaReservaDemograficaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (await repository.CensoExisteEntreLivosAsync(command.CensoReferencia, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
                $"Já existe uma Referência de reserva demográfica viva para o Censo '{command.CensoReferencia}'."));
        }

        Result<ReferenciaReservaDemografica> referenciaResult = ReferenciaReservaDemografica.Criar(
            command.CensoReferencia,
            command.PpiPercentual,
            command.QuilombolaPercentual,
            command.PcdPercentual,
            command.BaseLegal);

        if (referenciaResult.IsFailure)
        {
            return Result<Guid>.Failure(referenciaResult.Error!);
        }

        ReferenciaReservaDemografica referencia = referenciaResult.Value!;
        await repository.AdicionarAsync(referencia, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCensoConflict(constraint))
        {
            // Corrida entre CensoExisteEntreLivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo CensoJaExiste do
            // caminho não-race — 409 consistente, em vez de deixar o DbUpdateException
            // virar 500 no middleware global. O filtro do `when` garante que outras
            // exceções propagam intactas.
            return Result<Guid>.Failure(new DomainError(
                ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
                $"Já existe uma Referência de reserva demográfica viva para o Censo '{command.CensoReferencia}'."));
        }

        return Result<Guid>.Success(referencia.Id);
    }
}
