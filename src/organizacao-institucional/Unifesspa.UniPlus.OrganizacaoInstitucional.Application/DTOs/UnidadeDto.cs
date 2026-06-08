namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>Unidade</c>. Exposto pelos endpoints do
/// controller; suporta HATEOAS Level 1 via <c>_links</c>.
/// </summary>
public sealed record UnidadeDto(
    Guid Id,
    string Nome,
    string? Alias,
    string Slug,
    string Sigla,
    string Codigo,
    Guid? UnidadeSuperiorId,
    string Tipo,
    bool UnidadeAcademica,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string Origem,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
