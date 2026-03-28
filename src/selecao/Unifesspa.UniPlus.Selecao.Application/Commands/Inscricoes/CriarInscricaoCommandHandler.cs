namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using MediatR;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed class CriarInscricaoCommandHandler : IRequestHandler<CriarInscricaoCommand, Result<Guid>>
{
    private readonly IInscricaoRepository _inscricaoRepository;
    private readonly IEditalRepository _editalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarInscricaoCommandHandler(
        IInscricaoRepository inscricaoRepository,
        IEditalRepository editalRepository,
        IUnitOfWork unitOfWork)
    {
        _inscricaoRepository = inscricaoRepository;
        _editalRepository = editalRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CriarInscricaoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Edital? edital = await _editalRepository.ObterPorIdAsync(request.EditalId, cancellationToken).ConfigureAwait(false);
        if (edital is null)
            return Result<Guid>.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        bool existeInscricao = await _inscricaoRepository.ExisteInscricaoAtivaAsync(
            request.CandidatoId, request.EditalId, cancellationToken).ConfigureAwait(false);

        if (existeInscricao)
            return Result<Guid>.Failure(new DomainError("Inscricao.Duplicada", "Candidato já possui inscrição ativa neste processo seletivo."));

        Result<Inscricao> inscricaoResult = Inscricao.Criar(
            request.CandidatoId,
            request.EditalId,
            request.Modalidade,
            request.CodigoCursoPrimeiraOpcao);

        if (inscricaoResult.IsFailure)
            return Result<Guid>.Failure(inscricaoResult.Error!);

        await _inscricaoRepository.AdicionarAsync(inscricaoResult.Value!, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(inscricaoResult.Value!.Id);
    }
}
