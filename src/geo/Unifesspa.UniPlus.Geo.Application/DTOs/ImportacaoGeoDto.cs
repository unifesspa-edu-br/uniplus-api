namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// DTO de resposta HTTP de uma execução do ETL DNE (Story #674). Devolvido pelo
/// endpoint admin de acompanhamento (<c>GET /api/admin/geo/importacoes/{id}</c>);
/// suporta HATEOAS Level 1 via <c>_links</c> (ADR-0029). Sem PII.
/// </summary>
public sealed record ImportacaoGeoDto(
    Guid Id,
    string VersaoDataset,
    string Status,
    DateTimeOffset IniciadoEm,
    DateTimeOffset? ConcluidoEm,
    string DisparadoPor,
    string? Mensagem,
    RelatorioImportacaoDto? Relatorio)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
