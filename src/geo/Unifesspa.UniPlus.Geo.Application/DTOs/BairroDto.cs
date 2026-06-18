namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de item da coleção <c>GET /api/cidades/{codigoIbge}/bairros</c>.
/// Bairro não tem código IBGE próprio na fonte; é identificado pelo Id intra-banco
/// e pelo vínculo com a cidade-pai.
/// </summary>
public sealed record BairroDto(
    Guid Id,
    string Nome,
    string Uf,
    string CidadeCodigoIbge,
    decimal? Latitude,
    decimal? Longitude)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
