namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarTiposAtoPublicadoQuery"/>: lote projetado + pontos de continuação
/// opcionais para o controller construir os cursores prev/next. Não vaza entidades
/// de domínio.
/// </summary>
public sealed record ListarTiposAtoPublicadoResult(
    IReadOnlyList<TipoAtoPublicadoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
