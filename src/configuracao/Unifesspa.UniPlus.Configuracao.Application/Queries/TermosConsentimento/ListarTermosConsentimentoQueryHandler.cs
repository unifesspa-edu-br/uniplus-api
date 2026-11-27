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

        string? searchTerm = string.IsNullOrWhiteSpace(query.SearchTerm) ? null : query.SearchTerm;

        (IReadOnlyList<TermoConsentimento> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, searchTerm, cancellationToken)
            .ConfigureAwait(false);

        TermoConsentimentoResumoDto[] items = [.. itens.Select(t => t.ToResumoDto())];
        return new ListarTermosConsentimentoResult(items, anterior, proximo);
    }
}
