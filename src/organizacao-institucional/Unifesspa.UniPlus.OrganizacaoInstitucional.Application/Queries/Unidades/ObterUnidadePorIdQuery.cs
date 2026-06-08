namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;

public sealed record ObterUnidadePorIdQuery(Guid Id) : IQuery<UnidadeDto?>;
