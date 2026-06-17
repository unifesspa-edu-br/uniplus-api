namespace Unifesspa.UniPlus.Configuracao.Application.Queries.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarLocaisOfertaQuery"/>: lote de locais de oferta
/// projetados + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089).
/// </summary>
public sealed record ListarLocaisOfertaResult(
    IReadOnlyList<LocalOfertaDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
