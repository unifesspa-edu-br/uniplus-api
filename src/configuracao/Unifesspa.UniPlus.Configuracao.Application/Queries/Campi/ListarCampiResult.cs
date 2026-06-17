namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Campi;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCampiQuery"/>: lote de campi projetados +
/// âncoras opcionais para o controller construir os cursores prev/next
/// (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCampiResult(
    IReadOnlyList<CampusDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
