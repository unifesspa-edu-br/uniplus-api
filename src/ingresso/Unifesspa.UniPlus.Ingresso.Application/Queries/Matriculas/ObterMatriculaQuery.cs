namespace Unifesspa.UniPlus.Ingresso.Application.Queries.Matriculas;

using MediatR;

using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record ObterMatriculaQuery(Guid Id) : IRequest<Result<MatriculaDto>>;
