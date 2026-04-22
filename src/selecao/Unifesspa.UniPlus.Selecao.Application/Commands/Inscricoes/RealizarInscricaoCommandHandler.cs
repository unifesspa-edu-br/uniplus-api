namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using MediatR;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RealizarInscricaoCommandHandler : IRequestHandler<RealizarInscricaoCommand, Result<Guid>>
{
    private readonly IInscricaoRepository _inscricaoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RealizarInscricaoCommandHandler(IInscricaoRepository inscricaoRepository, IUnitOfWork unitOfWork)
    {
        _inscricaoRepository = inscricaoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(RealizarInscricaoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // RN01: Um CPF só pode realizar uma inscrição ativa por processo seletivo
        bool existeInscricao = await _inscricaoRepository
            .ExisteInscricaoAtivaAsync(request.CandidatoId, request.EditalId, cancellationToken)
            .ConfigureAwait(false);

        if (existeInscricao)
            return Result<Guid>.Failure(new DomainError("Inscricao.Duplicada", "Candidato já possui inscrição ativa neste processo seletivo."));

        Result<Inscricao> inscricaoResult = Inscricao.Criar(
            request.CandidatoId,
            request.EditalId,
            request.Modalidade,
            request.CodigoCursoPrimeiraOpcao);

        if (inscricaoResult.IsFailure)
            return Result<Guid>.Failure(inscricaoResult.Error!);

        Inscricao inscricao = inscricaoResult.Value!;

        if (!string.IsNullOrWhiteSpace(request.CodigoCursoSegundaOpcao))
            inscricao.DefinirSegundaOpcao(request.CodigoCursoSegundaOpcao);

        if (request.ListaEspera)
            inscricao.OptarPorListaEspera();

        await _inscricaoRepository.AdicionarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(inscricao.Id);
    }
}
