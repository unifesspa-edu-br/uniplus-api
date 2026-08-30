namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class CategoriaDocumentoMapping
{
    public static CategoriaDocumentoDto ToDto(this CategoriaDocumento categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        return new CategoriaDocumentoDto(
            categoria.Id,
            categoria.Codigo.Valor,
            categoria.Nome,
            categoria.Descricao,
            categoria.Ordem,
            categoria.CreatedAt);
    }
}
