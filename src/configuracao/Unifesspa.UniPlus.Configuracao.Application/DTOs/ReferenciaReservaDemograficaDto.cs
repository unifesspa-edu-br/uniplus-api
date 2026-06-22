namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>ReferenciaReservaDemografica</c>. Suporta
/// HATEOAS Level 1 via <c>_links</c> (ADR-0029). Os percentuais são expostos
/// como <c>decimal</c> (valor do value object <c>Percentual</c>).
/// </summary>
public sealed record ReferenciaReservaDemograficaDto(
    Guid Id,
    string CensoReferencia,
    decimal PpiPercentual,
    decimal QuilombolaPercentual,
    decimal PcdPercentual,
    string BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
