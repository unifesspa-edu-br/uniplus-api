namespace Unifesspa.UniPlus.Geo.Application.Queries.Estados;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Obtém um Estado vigente pela chave natural <paramref name="Uf"/> (2 letras,
/// normalizada para maiúsculas no reader). Retorna <see langword="null"/> quando
/// inexistente — o controller traduz para 404. O formato da UF é validado no
/// boundary (ADR-0031) antes do despacho.
/// </summary>
public sealed record ObterEstadoPorUfQuery(string Uf) : IQuery<EstadoDto?>;
