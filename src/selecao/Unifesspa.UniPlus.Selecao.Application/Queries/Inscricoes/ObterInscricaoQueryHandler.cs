namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using MediatR;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed class ObterInscricaoQueryHandler : IRequestHandler<ObterInscricaoQuery, Result<InscricaoDto>>
{
    private readonly IInscricaoRepository _inscricaoRepository;

    public ObterInscricaoQueryHandler(IInscricaoRepository inscricaoRepository)
    {
        _inscricaoRepository = inscricaoRepository;
    }

    public async Task<Result<InscricaoDto>> Handle(ObterInscricaoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Inscricao? inscricao = await _inscricaoRepository.ObterPorIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (inscricao is null)
            return Result<InscricaoDto>.Failure(new DomainError("Inscricao.NaoEncontrada", "Inscrição não encontrada."));

        InscricaoDto dto = new(
            inscricao.Id,
            inscricao.CandidatoId,
            inscricao.EditalId,
            inscricao.Modalidade.ToString(),
            inscricao.Status.ToString(),
            inscricao.CodigoCursoPrimeiraOpcao,
            inscricao.CodigoCursoSegundaOpcao,
            inscricao.ListaEspera,
            inscricao.NumeroInscricao,
            inscricao.CreatedAt);

        return Result<InscricaoDto>.Success(dto);
    }
}
