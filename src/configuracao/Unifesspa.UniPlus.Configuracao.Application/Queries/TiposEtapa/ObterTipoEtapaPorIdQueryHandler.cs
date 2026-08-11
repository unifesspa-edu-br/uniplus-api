namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterTipoEtapaPorIdQueryHandler
{
    public static async Task<TipoEtapaDto?> Handle(
        ObterTipoEtapaPorIdQuery query,
        ITipoEtapaRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        TipoEtapa? tipo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return tipo is { Ativo: true } ? tipo.ToDto() : null;
    }
}
