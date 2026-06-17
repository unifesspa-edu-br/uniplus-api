namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Campi;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterCampusPorIdQuery(Guid Id) : IQuery<CampusDto?>;
