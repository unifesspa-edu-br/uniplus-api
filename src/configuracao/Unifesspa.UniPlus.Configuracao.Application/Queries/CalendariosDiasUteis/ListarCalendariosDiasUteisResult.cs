namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCalendariosDiasUteisQuery"/>: lote de calendários
/// projetados + pontos de continuação opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCalendariosDiasUteisResult(
    IReadOnlyList<CalendarioDiasUteisResumoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
