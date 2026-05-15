namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.AreasOrganizacionais;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using DTOs;

/// <summary>
/// Query para listar todas as áreas organizacionais ativas. Sem paginação:
/// catálogo é bounded reference data (~5 áreas hoje, &lt;~20 no longo prazo),
/// exceção deliberada à ADR-0026.
/// </summary>
public sealed record ListarAreasOrganizacionaisAtivasQuery : IQuery<IReadOnlyList<AreaOrganizacionalDto>>;
