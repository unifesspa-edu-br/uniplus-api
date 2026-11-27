namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterReferenciaReservaDemograficaPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        ReferenciaReservaDemograficaErrorCodes.NaoEncontrada,
        "Referência de reserva demográfica não encontrada.");

    public static async Task<Result<ReferenciaReservaDemograficaDto>> Handle(
        ObterReferenciaReservaDemograficaPorIdQuery query,
        IReferenciaReservaDemograficaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        ReferenciaReservaDemografica? referencia = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return referencia is null
            ? Result<ReferenciaReservaDemograficaDto>.Failure(NaoEncontrado)
            : Result<ReferenciaReservaDemograficaDto>.Success(referencia.ToDto());
    }
}
