namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Snapshot da unidade ofertante aninhado no contrato HTTP da oferta de curso
/// (ADR-0061): <c>origemId</c> é a proveniência (Id da Unidade viva no ato da
/// criação) e <c>sigla</c>/<c>nome</c>/<c>tipo</c> são a cópia congelada — não
/// refletem mudanças posteriores na Unidade viva.
/// </summary>
public sealed record UnidadeOfertanteDto(
    Guid OrigemId,
    string Sigla,
    string Nome,
    string Tipo);
