namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>CalendarioDiasUteis</c>, com a lista completa de
/// dias não úteis — usado pelo <c>ObterPorId</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record CalendarioDiasUteisDto(
    Guid Id,
    string VersaoDataset,
    bool Vigente,
    IReadOnlyList<DiaNaoUtilDto> DiasNaoUteis,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
