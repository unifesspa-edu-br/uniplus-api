namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Projeção de leitura de <c>EtapaProcesso</c> (Story #758).
/// </summary>
public sealed record EtapaProcessoDto(
    Guid Id,
    string Nome,
    string Carater,
    decimal? Peso,
    decimal? NotaMinima,
    int? Ordem);
