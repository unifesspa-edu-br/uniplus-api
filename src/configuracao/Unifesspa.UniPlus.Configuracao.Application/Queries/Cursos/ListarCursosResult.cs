namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Cursos;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCursosQuery"/>: lote de cursos projetados +
/// âncoras opcionais para o controller construir os cursores prev/next
/// (ADR-0026 + ADR-0089). Cada âncora é o par chave de ordenação + <c>Id</c>
/// exigido pelo keyset ordenado (ADR-0094). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCursosResult(
    IReadOnlyList<CursoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
