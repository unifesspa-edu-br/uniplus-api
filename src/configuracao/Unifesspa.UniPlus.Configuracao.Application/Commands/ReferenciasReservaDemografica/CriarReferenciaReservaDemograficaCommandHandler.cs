namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarReferenciaReservaDemograficaCommand"/>
/// (convention-based Wolverine): valida o payload primeiro (422, sem I/O —
/// validação sempre vence I/O), confere a unicidade do Censo entre referências
/// vivas (409, DB), persiste e commita.
/// </summary>
public static class CriarReferenciaReservaDemograficaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarReferenciaReservaDemograficaCommand command,
        IReferenciaReservaDemograficaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ReferenciaReservaDemografica> referenciaResult = ReferenciaReservaDemografica.Criar(
            command.CensoReferencia,
            command.PpiPercentual,
            command.QuilombolaPercentual,
            command.PcdPercentual,
            command.BaseLegal);

        if (referenciaResult.IsFailure)
        {
            return Result<Guid>.ValidationFailure(referenciaResult.Errors);
        }

        ReferenciaReservaDemografica referencia = referenciaResult.Value!;

        if (await repository.CensoExisteEntreLivosAsync(referencia.CensoReferencia, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CensoJaExisteErro());
        }

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
            // exceções propagam intactas. Descarta o rastreamento da entidade que não
            // foi persistida, senão o save automático do Wolverine repetiria a
            // violação fora deste catch.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CensoJaExisteErro());
        }

        return Result<Guid>.Success(referencia.Id);
    }

    private static DomainError CensoJaExisteErro() =>
        new(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
            "Já existe uma Referência de reserva demográfica viva para o Censo informado.");
}
