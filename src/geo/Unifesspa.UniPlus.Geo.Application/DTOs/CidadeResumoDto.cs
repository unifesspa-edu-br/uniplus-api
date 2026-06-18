namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de item de coleção para <c>Cidade</c> (listagem <c>GET /api/cidades</c>).
/// Núcleo enxuto para popular combos; o detalhe completo vem em
/// <see cref="CidadeDetalheDto"/>. Suporta HATEOAS Level 1 via <c>_links</c>.
/// </summary>
public sealed record CidadeResumoDto(
    Guid Id,
    string CodigoIbge,
    string Nome,
    string Uf,
    string? Ddd)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
