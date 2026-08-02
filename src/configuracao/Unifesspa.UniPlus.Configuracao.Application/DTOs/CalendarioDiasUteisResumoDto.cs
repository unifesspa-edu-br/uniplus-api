namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>CalendarioDiasUteis</c> na listagem — sem os dias
/// não úteis (a listagem não carrega a coleção filha; use <c>ObterPorId</c> para o
/// dataset completo). Suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029).
/// </summary>
public sealed record CalendarioDiasUteisResumoDto(
    Guid Id,
    string VersaoDataset,
    bool Vigente,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
