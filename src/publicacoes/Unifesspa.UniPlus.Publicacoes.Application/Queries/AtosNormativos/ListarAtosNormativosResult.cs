namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarAtosNormativosQuery"/>: lote projetado + pontos de continuação
/// opcionais para o controller construir os cursores prev/next. Não vaza entidades
/// de domínio.
/// </summary>
public sealed record ListarAtosNormativosResult(
    IReadOnlyList<AtoNormativoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
