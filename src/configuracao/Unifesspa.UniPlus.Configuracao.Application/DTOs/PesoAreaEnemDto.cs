namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>PesoAreaEnem</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029). O grupo de área é exposto como <c>string</c> (valor
/// do value object <c>GrupoCurso</c>); os pesos e o corte como <c>decimal</c>.
/// </summary>
public sealed record PesoAreaEnemDto(
    Guid Id,
    string Resolucao,
    string GrupoCurso,
    decimal PesoRedacao,
    decimal PesoCienciasNatureza,
    decimal PesoCienciasHumanas,
    decimal PesoLinguagens,
    decimal PesoMatematica,
    decimal CorteRedacao,
    string BaseLegal,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
