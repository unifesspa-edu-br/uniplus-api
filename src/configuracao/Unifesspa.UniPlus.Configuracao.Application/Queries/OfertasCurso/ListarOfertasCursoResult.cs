namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarOfertasCursoQuery"/>: lote de ofertas
/// projetadas + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Cada âncora é o par chave de ordenação +
/// <c>Id</c> exigido pelo keyset ordenado (ADR-0094). Não vaza entidades de
/// domínio.
/// </summary>
public sealed record ListarOfertasCursoResult(
    IReadOnlyList<OfertaCursoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
