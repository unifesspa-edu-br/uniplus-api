namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarCondicoesAtendimentoQueryHandler
{
    public static async Task<ListarCondicoesAtendimentoResult> Handle(
        ListarCondicoesAtendimentoQuery query,
        ICondicaoAtendimentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<CondicaoAtendimentoEspecializado> itens, (string SortKey, Guid Id)? anterior, (string SortKey, Guid Id)? proximo) = await repository
            .ListarPaginadoAsync(query.AfterSortKey, query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        CondicaoAtendimentoDto[] items = [.. itens.Select(c => c.ToDto())];
        return new ListarCondicoesAtendimentoResult(items, anterior, proximo);
    }
}
