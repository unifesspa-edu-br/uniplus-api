namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Campi;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCampiQuery"/>: lote de campi projetados +
/// pontos de continuação opcionais para o controller construir os cursores prev/next
/// (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCampiResult(
    IReadOnlyList<CampusDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
