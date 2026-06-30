namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarTiposDocumentoQuery"/>: lote de tipos de
/// documento projetados + âncoras opcionais para o controller construir os
/// cursores prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarTiposDocumentoResult(
    IReadOnlyList<TipoDocumentoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
