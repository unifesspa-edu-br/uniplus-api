namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>TipoBanca</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record TipoBancaDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? FaseTipica,
    string? Descricao,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
