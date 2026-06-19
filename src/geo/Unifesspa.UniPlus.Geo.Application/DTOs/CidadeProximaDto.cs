namespace Unifesspa.UniPlus.Geo.Application.DTOs;

using System.Text.Json.Serialization;

/// <summary>
/// Item de resultado da consulta de proximidade de cidades
/// (<c>GET /api/cidades/proximas</c>): a Cidade encontrada dentro do raio mais a
/// <see cref="DistanciaKm"/> ao ponto consultado. É uma projeção de <em>ranking</em>
/// por distância (não um recurso canônico próprio) — os <c>_links</c> navegam ao
/// detalhe da cidade (ADR-0029), sem <c>self</c>.
/// </summary>
/// <remarks>
/// <see cref="DistanciaKm"/> vem de <c>ST_Distance</c> (metros sobre o esferoide) / 1000.
/// <see cref="Latitude"/>/<see cref="Longitude"/> espelham as colunas materializadas
/// <c>decimal(9,6)</c> (ADR-0091); só entram cidades cujas três coordenadas
/// (coordenada geográfica + lat + long) estão preenchidas.
/// </remarks>
public sealed record CidadeProximaDto(
    Guid Id,
    string CodigoIbge,
    string Nome,
    string Uf,
    decimal Latitude,
    decimal Longitude,
    double DistanciaKm)
{
    [JsonPropertyName("_links")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Links { get; init; }
}
