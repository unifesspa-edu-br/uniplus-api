namespace Unifesspa.UniPlus.Geo.Application.Abstractions;

using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Leitor read-side de consultas geoespaciais de proximidade (ADR-0091) sobre o
/// reference data vigente (ADR-0092): dado um ponto (latitude/longitude em graus
/// decimais) e um raio, devolve as entidades dentro do raio ordenadas por distância
/// crescente (top-N). Filtro por <c>ST_DWithin</c> (índice GIST) e ordenação por
/// <c>ST_Distance</c> sobre <c>geography</c> (metros).
/// </summary>
public interface IGeoProximidadeReader
{
    /// <summary>Cidades vigentes a até <paramref name="raioKm"/> do ponto, ordenadas por distância.</summary>
    Task<IReadOnlyList<CidadeProximaDto>> BuscarCidadesProximasAsync(
        double latitude,
        double longitude,
        double raioKm,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>Logradouros vigentes (em cidade vigente) a até <paramref name="raioKm"/> do ponto, ordenados por distância.</summary>
    Task<IReadOnlyList<LogradouroProximoDto>> BuscarLogradourosProximosAsync(
        double latitude,
        double longitude,
        double raioKm,
        int limit,
        CancellationToken cancellationToken);
}
