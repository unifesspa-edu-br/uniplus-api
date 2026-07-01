namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposBanca;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarTiposBancaQuery"/>: lote de tipos de banca
/// projetados + âncoras opcionais para o controller construir os cursores prev/next
/// (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarTiposBancaResult(
    IReadOnlyList<TipoBancaDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
