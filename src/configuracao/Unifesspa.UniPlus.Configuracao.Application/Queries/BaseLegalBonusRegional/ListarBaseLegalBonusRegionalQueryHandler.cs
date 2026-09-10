namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarBaseLegalBonusRegionalQueryHandler
{
    public static async Task<ListarBaseLegalBonusRegionalResult> Handle(
        ListarBaseLegalBonusRegionalQuery query,
        IBaseLegalBonusRegionalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        (IReadOnlyList<Domain.Entities.BaseLegalBonusRegional> itens, Guid? anterior, Guid? proximo) = await repository.ListarPaginadoAsync(
            query.AfterId,
            query.Limit,
            query.Direction,
            cancellationToken).ConfigureAwait(false);

        return new ListarBaseLegalBonusRegionalResult(
            itens.Select(i => i.ToDto()).ToList().AsReadOnly(),
            anterior,
            proximo);
    }
}
