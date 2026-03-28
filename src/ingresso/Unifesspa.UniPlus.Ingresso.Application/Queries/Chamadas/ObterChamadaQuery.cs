namespace Unifesspa.UniPlus.Ingresso.Application.Queries.Chamadas;

using MediatR;

using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record ObterChamadaQuery(Guid Id) : IRequest<Result<ChamadaDto>>;
