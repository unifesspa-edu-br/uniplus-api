namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Matriculas;

using MediatR;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class IniciarMatriculaCommandHandler : IRequestHandler<IniciarMatriculaCommand, Result<Guid>>
{
    private readonly IMatriculaRepository _matriculaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public IniciarMatriculaCommandHandler(IMatriculaRepository matriculaRepository, IUnitOfWork unitOfWork)
    {
        _matriculaRepository = matriculaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(IniciarMatriculaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Matricula matricula = Matricula.Criar(request.ConvocacaoId, request.CandidatoId, request.CodigoCurso);

        await _matriculaRepository.AdicionarAsync(matricula, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(matricula.Id);
    }
}
