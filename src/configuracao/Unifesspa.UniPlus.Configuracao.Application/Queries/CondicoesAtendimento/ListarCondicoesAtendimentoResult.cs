namespace Unifesspa.UniPlus.Configuracao.Application.Queries.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Resultado da <see cref="ListarCondicoesAtendimentoQuery"/>: lote de condições
/// projetadas + âncoras opcionais para o controller construir os cursores
/// prev/next (ADR-0026 + ADR-0089). Não vaza entidades de domínio.
/// </summary>
public sealed record ListarCondicoesAtendimentoResult(
    IReadOnlyList<CondicaoAtendimentoDto> Items,
    Guid? AnteriorAfterId,
    Guid? ProximoAfterId);
