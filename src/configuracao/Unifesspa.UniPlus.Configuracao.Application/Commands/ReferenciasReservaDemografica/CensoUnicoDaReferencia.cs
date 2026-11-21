namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Regra de que o Censo identifica uma única Referência de reserva demográfica viva: a conferência antes de gravar e a
/// tradução da corrida que só o índice único parcial do banco enxerga.
/// </summary>
internal static class CensoUnicoDaReferencia
{
    private static DomainError CensoJaExiste() =>
        new(ReferenciaReservaDemograficaErrorCodes.CensoJaExiste,
            "Já existe uma Referência de reserva demográfica viva para o Censo informado.");

    /// <summary>
    /// Recusa o Censo que já pertence a outra referência viva; <paramref name="referenciaEditadaId"/> exclui da busca a
    /// própria referência em edição.
    /// </summary>
    public static async Task<Result> ConferirAsync(
        string censo,
        Guid? referenciaEditadaId,
        IReferenciaReservaDemograficaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        return await repository.CensoExisteEntreLivosAsync(censo, referenciaEditadaId, cancellationToken).ConfigureAwait(false)
            ? Result.Failure(CensoJaExiste())
            : Result.Success();
    }

    /// <remarks>
    /// A colisão só é conferida quando o Censo muda. A comparação é ordinal, para casar com o repositório e com o
    /// índice único parcial: ambos comparam o valor bruto.
    /// </remarks>
    public static async Task<Result> ConferirNaEdicaoAsync(
        ReferenciaReservaDemografica referencia,
        string? censoInformado,
        IReferenciaReservaDemograficaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(referencia);

        return censoInformado is { } censo
            && !string.Equals(censo.Trim(), referencia.CensoReferencia, StringComparison.Ordinal)
            ? await ConferirAsync(censo, referencia.Id, repository, cancellationToken).ConfigureAwait(false)
            : Result.Success();
    }

    /// <remarks>
    /// Corrida entre a conferência do Censo e a gravação: o índice único parcial dispara 23505 e o resultado é o mesmo
    /// <c>CensoJaExiste</c> do caminho sem corrida (409 consistente em vez de 500). O rastreamento é descartado porque
    /// o save automático do Wolverine repetiria a violação fora deste catch. O filtro <c>when</c> deixa as demais
    /// exceções propagarem.
    /// </remarks>
    public static async Task<Result> GravarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
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

            return Result.Failure(CensoJaExiste());
        }
    }
}
