namespace Unifesspa.UniPlus.Selecao.Application.Queries.Acompanhamento;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

public static class ListarProcessosAcompanhamentoQueryHandler
{
    public static async Task<ListarProcessosAcompanhamentoResult> Handle(
        ListarProcessosAcompanhamentoQuery query,
        IProcessoSeletivoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<ProcessoSeletivo> itens, Guid? anterior, Guid? proximo) = await repository
            .ListarPaginadoAsync(query.AfterId, query.Limit, query.Direction, cancellationToken)
            .ConfigureAwait(false);

        return new ListarProcessosAcompanhamentoResult(
            [.. itens.Select(static p => new ProcessoAcompanhamentoResumoDto(p.Id, p.Nome, p.Status))],
            anterior,
            proximo);
    }
}
