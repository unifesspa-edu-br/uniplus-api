namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CategoriasDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCategoriasDocumentoQuery"/>: o catálogo
/// projetado em DTO, na ordem de exibição. Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCategoriasDocumentoResult(IReadOnlyList<CategoriaDocumentoDto> Itens);
