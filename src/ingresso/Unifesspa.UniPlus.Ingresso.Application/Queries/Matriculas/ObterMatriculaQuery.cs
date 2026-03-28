namespace Unifesspa.UniPlus.Ingresso.Application.Queries.Matriculas;

using MediatR;

using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record ObterMatriculaQuery(Guid Id) : IRequest<Result<MatriculaDto>>;
