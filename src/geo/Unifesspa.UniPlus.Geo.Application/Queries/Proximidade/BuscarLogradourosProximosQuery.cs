namespace Unifesspa.UniPlus.Geo.Application.Queries.Proximidade;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Busca os logradouros vigentes (em cidade vigente) dentro de <see cref="RaioKm"/>
/// do ponto (<see cref="Latitude"/>, <see cref="Longitude"/>), ordenados por distância
/// crescente, limitados ao top-<see cref="Limit"/> (ranking por distância — sem cursor).
/// </summary>
public sealed record BuscarLogradourosProximosQuery(
    double Latitude,
    double Longitude,
    double RaioKm,
    int Limit) : IQuery<IReadOnlyList<LogradouroProximoDto>>;
