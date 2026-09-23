namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>Curso</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O grupo de área do ENEM vem com código e rótulo
/// (Resolução nº 805/2024/Consepe); nulo quando o curso não classifica por área.
/// </summary>
public sealed record CursoDto(
    Guid Id,
    string Codigo,
    string Nome,
    string Grau,
    string NivelEnsino,
    GrupoAreaEnemDto? GrupoAreaEnem,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
