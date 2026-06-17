namespace Unifesspa.UniPlus.Configuracao.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP para <c>Campus</c>. Suporta HATEOAS Level 1 via
/// <c>_links</c> (ADR-0029).
/// </summary>
public sealed record CampusDto(
    Guid Id,
    string Sigla,
    string Nome,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    string? CidadeOrigem,
    DateTimeOffset? CidadeDisplayAtualizadoEm,
    string? Endereco,
    string? Cep,
    decimal? Latitude,
    decimal? Longitude,
    string? CodigoEmec,
    DateTimeOffset CriadoEm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
