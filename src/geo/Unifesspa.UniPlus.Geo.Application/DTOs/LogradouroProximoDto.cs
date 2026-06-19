namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Item de resultado da consulta de proximidade de logradouros
/// (<c>GET /api/logradouros/proximos</c>): o Logradouro encontrado dentro do raio,
/// enriquecido com a cidade (nome + código IBGE) e a <see cref="DistanciaKm"/> ao
/// ponto consultado. Projeção de <em>ranking</em> por distância — os <c>_links</c>
/// navegam à cidade e ao lookup de CEP (ADR-0029), sem <c>self</c>.
/// </summary>
/// <remarks>
/// <see cref="DistanciaKm"/> vem de <c>ST_Distance</c> (metros) / 1000;
/// <see cref="Latitude"/>/<see cref="Longitude"/> espelham as colunas
/// <c>decimal(9,6)</c> (ADR-0091). Só entram logradouros com coordenada e lat/long
/// preenchidos, vinculados a uma cidade vigente.
/// </remarks>
public sealed record LogradouroProximoDto(
    Guid Id,
    string Cep,
    string? Tipo,
    string Nome,
    string CidadeNome,
    string CidadeCodigoIbge,
    string Uf,
    decimal Latitude,
    decimal Longitude,
    double DistanciaKm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
