namespace Unifesspa.UniPlus.Geo.Application.Queries.Cep;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Resolve um CEP em endereço estruturado. <paramref name="Cep"/> chega já
/// normalizado (8 dígitos, sem máscara) — o formato é validado no boundary
/// (ADR-0031) antes do despacho. Retorna <see langword="null"/> quando nada casa —
/// o controller traduz para 404.
/// </summary>
public sealed record ResolverCepQuery(string Cep) : IQuery<CepResolvidoDto?>;
