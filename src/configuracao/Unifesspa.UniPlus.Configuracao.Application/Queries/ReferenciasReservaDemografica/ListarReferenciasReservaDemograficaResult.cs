namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ReferenciasReservaDemografica;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarReferenciasReservaDemograficaQuery"/>: lote de
/// referências projetadas + âncoras opcionais para o controller construir os
/// cursores prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarReferenciasReservaDemograficaResult(
    IReadOnlyList<ReferenciaReservaDemograficaDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
