namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterTipoDocumentoPorIdQueryHandler
{
    private static readonly DomainError NaoEncontrado = new(
        TipoDocumentoErrorCodes.NaoEncontrado,
        "Tipo de documento não encontrado.");

    public static async Task<Result<TipoDocumentoDto>> Handle(
        ObterTipoDocumentoPorIdQuery query,
        ITipoDocumentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        TipoDocumento? tipo = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return tipo is null
            ? Result<TipoDocumentoDto>.Failure(NaoEncontrado)
            : Result<TipoDocumentoDto>.Success(tipo.ToDto());
    }
}
