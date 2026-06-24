namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterPesoAreaEnemPorIdQueryHandler
{
    public static async Task<PesoAreaEnemDto?> Handle(
        ObterPesoAreaEnemPorIdQuery query,
        IPesoAreaEnemRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        PesoAreaEnem? peso = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return peso?.ToDto();
    }
}
