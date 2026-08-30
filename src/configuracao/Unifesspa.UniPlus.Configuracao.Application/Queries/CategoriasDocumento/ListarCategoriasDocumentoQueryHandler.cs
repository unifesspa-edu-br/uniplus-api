namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CategoriasDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

public static class ListarCategoriasDocumentoQueryHandler
{
    public static async Task<ListarCategoriasDocumentoResult> Handle(
        ListarCategoriasDocumentoQuery query,
        ICategoriaDocumentoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<CategoriaDocumento> categorias = await repository
            .ListarVivasOrdenadasAsync(cancellationToken)
            .ConfigureAwait(false);

        CategoriaDocumentoDto[] itens = [.. categorias.Select(c => c.ToDto())];
        return new ListarCategoriasDocumentoResult(itens);
    }
}
