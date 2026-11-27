namespace Unifesspa.UniPlus.Configuracao.Application.Queries.FasesCanonicas;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterFaseCanonicaPorIdQuery(Guid Id) : IQuery<Result<FaseCanonicaDto>>;
