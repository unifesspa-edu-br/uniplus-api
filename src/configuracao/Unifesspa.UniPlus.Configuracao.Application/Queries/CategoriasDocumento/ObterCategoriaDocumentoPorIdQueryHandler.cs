namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CategoriasDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterCategoriaDocumentoPorIdQueryHandler
{
    public static async Task<Result<CategoriaDocumentoDto>> Handle(
        ObterCategoriaDocumentoPorIdQuery query,
        ICategoriaDocumentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        CategoriaDocumento? categoria = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return categoria is null
            ? Result<CategoriaDocumentoDto>.Failure(new DomainError(CategoriaDocumentoErrorCodes.NaoEncontrada, "Categoria de documento não encontrada."))
            : Result<CategoriaDocumentoDto>.Success(categoria.ToDto());
    }
}
