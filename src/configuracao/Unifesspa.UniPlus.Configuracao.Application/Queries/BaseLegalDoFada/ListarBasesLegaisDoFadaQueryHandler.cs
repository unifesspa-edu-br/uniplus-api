namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarBasesLegaisDoFadaQueryHandler
{
    public static async Task<ListarBasesLegaisDoFadaResult> Handle(
        ListarBasesLegaisDoFadaQuery query,
        IBaseLegalDoFadaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<Domain.Entities.BaseLegalDoFada> itens, Guid? anterior, Guid? proximo) = await repository.ListarPaginadoAsync(
            query.AfterId,
            query.Limit,
            query.Direction,
            cancellationToken).ConfigureAwait(false);

        return new ListarBasesLegaisDoFadaResult(
            itens.Select(i => i.ToDto()).ToList().AsReadOnly(),
            anterior,
            proximo);
    }
}
