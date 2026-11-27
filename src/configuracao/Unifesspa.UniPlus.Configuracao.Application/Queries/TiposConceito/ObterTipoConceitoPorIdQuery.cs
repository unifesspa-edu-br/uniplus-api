namespace Unifesspa.UniPlus.Configuracao.Application.Queries.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterTipoConceitoPorIdQuery(Guid Id) : IQuery<Result<TipoConceitoDto>>;
