namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalBonusRegional;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterBaseLegalBonusRegionalPorIdQuery(Guid Id) : IQuery<BaseLegalBonusRegionalDto?>;
