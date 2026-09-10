namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterBaseLegalBonusRegionalPorIdQueryHandler
{
    public static async Task<BaseLegalBonusRegionalDto?> Handle(
        ObterBaseLegalBonusRegionalPorIdQuery query,
        IBaseLegalBonusRegionalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        BaseLegalBonusRegional? entity = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return entity?.ToDto();
    }
}
