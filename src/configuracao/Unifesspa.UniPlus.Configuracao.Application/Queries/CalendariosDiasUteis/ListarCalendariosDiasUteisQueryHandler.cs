namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarCalendariosDiasUteisQueryHandler
{
    public static async Task<ListarCalendariosDiasUteisResult> Handle(
        ListarCalendariosDiasUteisQuery query,
        ICalendarioDiasUteisRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<CalendarioDiasUteis> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        CalendarioDiasUteisResumoDto[] items = [.. itens.Select(c => c.ToResumoDto())];
        return new ListarCalendariosDiasUteisResult(items, anteriorAfterId, proximoAfterId);
    }
}
