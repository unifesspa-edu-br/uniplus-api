namespace Unifesspa.UniPlus.Geo.Application.Queries.Cidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Obtém uma Cidade vigente pela chave natural <paramref name="CodigoIbge"/> (7
/// dígitos), com territorial embutido + indicador 1:1. Retorna
/// <see langword="null"/> quando inexistente — o controller traduz para 404. O
/// formato é validado no boundary (ADR-0031) antes do despacho.
/// </summary>
public sealed record ObterCidadePorCodigoIbgeQuery(string CodigoIbge) : IQuery<CidadeDetalheDto?>;
