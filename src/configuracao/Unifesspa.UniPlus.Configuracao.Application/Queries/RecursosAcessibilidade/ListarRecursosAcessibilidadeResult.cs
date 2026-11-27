namespace Unifesspa.UniPlus.Configuracao.Application.Queries.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarRecursosAcessibilidadeQuery"/>: lote de recursos
/// de acessibilidade projetados + pontos de continuação opcionais para o controller construir os
/// cursores prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarRecursosAcessibilidadeResult(
    IReadOnlyList<RecursoAcessibilidadeDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
