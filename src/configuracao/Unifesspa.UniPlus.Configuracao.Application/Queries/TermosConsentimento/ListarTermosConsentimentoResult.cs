namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TermosConsentimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarTermosConsentimentoQuery"/>: lote de termos
/// projetados + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarTermosConsentimentoResult(
    IReadOnlyList<TermoConsentimentoResumoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
