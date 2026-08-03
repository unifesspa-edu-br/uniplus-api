namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarTermosConsentimentoQueryHandler
{
    public static async Task<ListarTermosConsentimentoResult> Handle(
        ListarTermosConsentimentoQuery query,
        ITermoConsentimentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<TermoConsentimento> itens, Guid? anteriorAfterId, Guid? proximoAfterId) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        TermoConsentimentoResumoDto[] items = [.. itens.Select(t => t.ToResumoDto())];
        return new ListarTermosConsentimentoResult(items, anteriorAfterId, proximoAfterId);
    }
}
