namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PrecedenciasFase;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarPrecedenciasFaseQuery"/>: lote de arestas
/// projetadas + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarPrecedenciasFaseResult(
    IReadOnlyList<PrecedenciaFaseDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
