namespace Unifesspa.UniPlus.Geo.Application.Queries.Estados;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarEstadosQuery"/>: lote de Estados já projetados +
/// âncoras opcionais para o controller construir os cursores de página
/// anterior/próxima (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarEstadosResult(
    IReadOnlyList<EstadoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
