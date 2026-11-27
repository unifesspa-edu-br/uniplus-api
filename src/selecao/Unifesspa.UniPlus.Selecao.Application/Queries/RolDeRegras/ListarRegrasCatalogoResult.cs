namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado paginado do <see cref="ListarRegrasCatalogoQuery"/> (ADR-0089). Os pontos de continuação são os
/// identificadores de fronteira da página; o controller emite os cursores cifrados e o header
/// <c>Link</c>.
/// </summary>
public sealed record ListarRegrasCatalogoResult(
    IReadOnlyList<RegraCatalogoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
