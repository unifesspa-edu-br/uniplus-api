namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.AreasOrganizacionais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using DTOs;

/// <summary>
/// Query para obter uma área organizacional por seu código.
/// O handler normaliza o código via <c>AreaCodigo.From</c>; entrada inválida
/// retorna <see langword="null"/> (controller mapeia para 404).
/// </summary>
public sealed record ObterAreaOrganizacionalPorCodigoQuery(string Codigo) : IQuery<AreaOrganizacionalDto?>;
