namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterCalendarioDiasUteisPorIdQueryHandler
{
    public static async Task<CalendarioDiasUteisDto?> Handle(
        ObterCalendarioDiasUteisPorIdQuery query,
        ICalendarioDiasUteisRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        CalendarioDiasUteis? calendario = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return calendario?.ToDto();
    }
}
