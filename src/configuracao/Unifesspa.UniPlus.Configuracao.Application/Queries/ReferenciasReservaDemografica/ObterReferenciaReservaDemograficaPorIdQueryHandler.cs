namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterReferenciaReservaDemograficaPorIdQueryHandler
{
    public static async Task<ReferenciaReservaDemograficaDto?> Handle(
        ObterReferenciaReservaDemograficaPorIdQuery query,
        IReferenciaReservaDemograficaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        ReferenciaReservaDemografica? referencia = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return referencia?.ToDto();
    }
}
