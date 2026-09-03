namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterTipoProcessoPorIdQueryHandler
{
    public static async Task<TipoProcessoDto?> Handle(
        ObterTipoProcessoPorIdQuery query,
        ITipoProcessoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        TipoProcesso? tipo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null || (query.ApenasAtivos && !tipo.Ativo))
        {
            return null;
        }

        return tipo.ToDto();
    }
}
