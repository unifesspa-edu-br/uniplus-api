namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarFasesCanonicasQuery"/>: lote de fases projetadas +
/// âncoras opcionais para o controller construir os cursores prev/next (ADR-0026 +
/// ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarFasesCanonicasResult(
    IReadOnlyList<FaseCanonicaDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
