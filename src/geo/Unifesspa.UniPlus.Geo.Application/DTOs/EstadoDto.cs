namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para um <c>Estado</c> (UF). Reference data público
/// (read-only); suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029). A
/// <c>Coordenada</c> PostGIS não vaza no contrato — só <c>Latitude</c>/<c>Longitude</c>.
/// </summary>
public sealed record EstadoDto(
    Guid Id,
    string Uf,
    string Nome,
    string? Regiao,
    string? Capital,
    string? CodigoIbge,
    decimal? Latitude,
    decimal? Longitude)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
