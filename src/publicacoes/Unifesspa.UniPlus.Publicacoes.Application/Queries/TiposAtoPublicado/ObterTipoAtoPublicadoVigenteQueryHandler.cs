namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ObterTipoAtoPublicadoVigenteQueryHandler
{
    public static async Task<Result<TipoAtoPublicadoDto>> Handle(
        ObterTipoAtoPublicadoVigenteQuery query,
        ITipoAtoPublicadoRepository repository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateOnly data = query.Data
            ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.Date);

        TipoAtoPublicado? tipo = await repository
            .ObterVigenteAsync(query.Codigo, data, cancellationToken)
            .ConfigureAwait(false);

        return LeituraDoTipoAtoPublicado.DtoOuNaoEncontrado(tipo);
    }
}
