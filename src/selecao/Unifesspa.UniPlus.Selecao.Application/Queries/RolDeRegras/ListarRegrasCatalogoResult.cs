namespace Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;

using System.Collections.Generic;

using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Resultado paginado do <see cref="ListarRegrasCatalogoQuery"/> (ADR-0089). As âncoras são os
/// identificadores de fronteira da janela; o controller emite os cursores cifrados e o header
/// <c>Link</c>.
/// </summary>
public sealed record ListarRegrasCatalogoResult(
    IReadOnlyList<RegraCatalogoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
