namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Matriculas;

using MediatR;

using Unifesspa.UniPlus.Kernel.Results;

public sealed record IniciarMatriculaCommand(
    Guid ConvocacaoId,
    Guid CandidatoId,
    string CodigoCurso) : IRequest<Result<Guid>>;
