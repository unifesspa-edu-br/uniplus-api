namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposProcesso;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

public sealed record ObterTipoProcessoPorIdQuery(Guid Id) : IQuery<TipoProcessoDto?>;
