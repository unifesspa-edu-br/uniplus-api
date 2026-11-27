namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado paginado do <see cref="ListarMotivosDeDecisaoQuery"/> (ADR-0089). Os pontos de continuação são os
/// <c>Id</c> de fronteira da página; o controller emite os cursores e o header <c>Link</c>.
/// </summary>
public sealed record ListarMotivosDeDecisaoResult(
    IReadOnlyList<MotivoDeDecisaoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
