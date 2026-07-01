namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>Modalidade</c> de concorrência. Suporta HATEOAS
/// Level 1 via <c>_links</c> (ADR-0029). Os enums são expostos como tokens
/// canônicos UPPER_SNAKE; os argumentos de remanejamento como campos planos.
/// </summary>
public sealed record ModalidadeDto(
    Guid Id,
    string Codigo,
    string? Descricao,
    string NaturezaLegal,
    string ComposicaoVagas,
    string? ComposicaoOrigem,
    string? RegraRemanejamento,
    string? RemanejamentoDestino,
    string? RemanejamentoPar,
    string? RemanejamentoFallback,
    IReadOnlyList<string> CriteriosCumulativos,
    string? AcaoQuandoIndeferido,
    string? BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
