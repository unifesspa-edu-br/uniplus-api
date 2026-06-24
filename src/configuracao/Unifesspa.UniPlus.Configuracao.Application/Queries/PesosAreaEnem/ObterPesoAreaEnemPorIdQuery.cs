namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterPesoAreaEnemPorIdQuery(Guid Id) : IQuery<PesoAreaEnemDto?>;
