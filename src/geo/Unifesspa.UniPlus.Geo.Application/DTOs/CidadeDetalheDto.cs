namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de detalhe para <c>Cidade</c> (<c>GET /api/cidades/{codigoIbge}</c>):
/// núcleo + territorial IBGE embutido (meso/micro/região intermediária e imediata)
/// + indicador socioeconômico 1:1 (<see cref="CidadeIndicadorDto"/>, pode ser
/// <see langword="null"/>). Suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029).
/// </summary>
public sealed record CidadeDetalheDto(
    Guid Id,
    string CodigoIbge,
    string Nome,
    string Uf,
    string? Ddd,
    decimal? Latitude,
    decimal? Longitude,
    string? MesorregiaoNome,
    string? MicrorregiaoNome,
    string? RegiaoIntermediariaNome,
    string? RegiaoImediataNome,
    CidadeIndicadorDto? Indicador)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
