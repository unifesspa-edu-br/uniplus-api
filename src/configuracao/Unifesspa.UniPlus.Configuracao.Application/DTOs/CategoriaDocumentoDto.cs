namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>CategoriaDocumento</c>. Suporta HATEOAS Level 1
/// via <c>_links</c> (ADR-0029). O código é exposto como <c>string</c> (token
/// canônico UPPER_SNAKE).
/// </summary>
public sealed record CategoriaDocumentoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    int Ordem,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
