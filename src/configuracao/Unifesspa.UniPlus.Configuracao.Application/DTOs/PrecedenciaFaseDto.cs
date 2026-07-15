namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>PrecedenciaFase</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record PrecedenciaFaseDto(
    Guid Id,
    string AntecessoraCodigo,
    string SucessoraCodigo,
    bool PermiteSobreposicao,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
