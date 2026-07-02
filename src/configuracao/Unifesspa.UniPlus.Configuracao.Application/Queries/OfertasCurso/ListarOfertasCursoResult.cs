namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarOfertasCursoQuery"/>: lote de ofertas
/// projetadas + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarOfertasCursoResult(
    IReadOnlyList<OfertaCursoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
