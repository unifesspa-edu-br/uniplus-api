namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarFasesCanonicasQuery"/>: lote de fases projetadas +
/// pontos de continuação opcionais para o controller construir os cursores prev/next (ADR-0026 +
/// ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarFasesCanonicasResult(
    IReadOnlyList<FaseCanonicaDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
