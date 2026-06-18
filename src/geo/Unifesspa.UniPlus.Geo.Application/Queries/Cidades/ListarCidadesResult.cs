namespace Unifesspa.UniPlus.Geo.Application.Queries.Cidades;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCidadesQuery"/>: lote de Cidades já projetadas em
/// resumo + âncoras opcionais para o controller construir os cursores de página
/// anterior/próxima (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCidadesResult(
    IReadOnlyList<CidadeResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
