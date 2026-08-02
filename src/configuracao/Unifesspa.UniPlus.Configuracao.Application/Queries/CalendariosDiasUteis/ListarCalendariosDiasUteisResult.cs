namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CalendariosDiasUteis;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCalendariosDiasUteisQuery"/>: lote de datasets
/// projetados + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCalendariosDiasUteisResult(
    IReadOnlyList<CalendarioDiasUteisResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
