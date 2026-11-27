namespace Unifesspa.UniPlus.Configuracao.Application.Queries.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterBaseLegalDoFadaPorIdQuery(Guid Id) : IQuery<Result<BaseLegalDoFadaDto>>;
