namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarTiposDocumentoQuery"/>: lote de tipos de
/// documento projetados + pontos de continuação opcionais para o controller construir os
/// cursores prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarTiposDocumentoResult(
    IReadOnlyList<TipoDocumentoDto> Items,
    (string SortKey, Guid Id)? Anterior,
    (string SortKey, Guid Id)? Proximo);
