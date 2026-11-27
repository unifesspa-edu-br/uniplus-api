namespace Unifesspa.UniPlus.Configuracao.Application.Queries.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarLocaisOfertaQuery"/>: lote de locais de oferta
/// projetados + pontos de continuação opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089).
/// </summary>
public sealed record ListarLocaisOfertaResult(
    IReadOnlyList<LocalOfertaDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
