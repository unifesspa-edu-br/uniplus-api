namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarPesosAreaEnemQuery"/>: lote de linhas de pesos
/// projetadas + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarPesosAreaEnemResult(
    IReadOnlyList<PesoAreaEnemDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
