namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarTiposDeficienciaQuery"/>: lote de tipos de
/// deficiência projetados + âncoras opcionais para o controller construir os
/// cursores prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarTiposDeficienciaResult(
    IReadOnlyList<TipoDeficienciaDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
