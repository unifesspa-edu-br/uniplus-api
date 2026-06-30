namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ObterTipoDocumentoPorIdQueryHandler
{
    public static async Task<TipoDocumentoDto?> Handle(
        ObterTipoDocumentoPorIdQuery query,
        ITipoDocumentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TipoDocumento? tipo = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return tipo?.ToDto();
    }
}
