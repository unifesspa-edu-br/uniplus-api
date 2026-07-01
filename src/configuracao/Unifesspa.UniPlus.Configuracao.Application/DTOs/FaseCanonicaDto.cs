namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>FaseCanonica</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O <c>DonoTipico</c> é exposto como token canônico
/// UPPER_SNAKE.
/// </summary>
public sealed record FaseCanonicaDto(
    Guid Id,
    string Codigo,
    string Nome,
    string? Descricao,
    string DonoTipico,
    bool AgrupaEtapas,
    bool PermiteComplementacao,
    string? BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
