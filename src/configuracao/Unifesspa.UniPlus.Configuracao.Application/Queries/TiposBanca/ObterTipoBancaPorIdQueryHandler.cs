namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterTipoBancaPorIdQueryHandler
{
    public static async Task<TipoBancaDto?> Handle(
        ObterTipoBancaPorIdQuery query,
        ITipoBancaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TipoBanca? banca = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return banca?.ToDto();
    }
}
