namespace Unifesspa.UniPlus.Ingresso.Application.Queries.Matriculas;

using MediatR;

using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed class ObterMatriculaQueryHandler : IRequestHandler<ObterMatriculaQuery, Result<MatriculaDto>>
{
    private readonly IMatriculaRepository _matriculaRepository;

    public ObterMatriculaQueryHandler(IMatriculaRepository matriculaRepository)
    {
        _matriculaRepository = matriculaRepository;
    }

    public async Task<Result<MatriculaDto>> Handle(ObterMatriculaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Matricula? matricula = await _matriculaRepository.ObterPorIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (matricula is null)
            return Result<MatriculaDto>.Failure(new DomainError("Matricula.NaoEncontrada", "Matrícula não encontrada."));

        MatriculaDto dto = new(
            matricula.Id,
            matricula.ConvocacaoId,
            matricula.CandidatoId,
            matricula.Status.ToString(),
            matricula.CodigoCurso,
            matricula.Observacoes,
            matricula.CreatedAt);

        return Result<MatriculaDto>.Success(dto);
    }
}
