namespace Unifesspa.UniPlus.Ingresso.Application.Queries.Chamadas;

using MediatR;

using Unifesspa.UniPlus.Ingresso.Application.DTOs;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ObterChamadaQueryHandler : IRequestHandler<ObterChamadaQuery, Result<ChamadaDto>>
{
    private readonly IChamadaRepository _chamadaRepository;

    public ObterChamadaQueryHandler(IChamadaRepository chamadaRepository)
    {
        _chamadaRepository = chamadaRepository;
    }

    public async Task<Result<ChamadaDto>> Handle(ObterChamadaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Chamada? chamada = await _chamadaRepository.ObterPorIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (chamada is null)
            return Result<ChamadaDto>.Failure(new DomainError("Chamada.NaoEncontrada", "Chamada não encontrada."));

        ChamadaDto dto = new(
            chamada.Id,
            chamada.EditalId,
            chamada.Numero,
            chamada.Status.ToString(),
            chamada.DataPublicacao,
            chamada.PrazoManifestacao,
            chamada.CreatedAt);

        return Result<ChamadaDto>.Success(dto);
    }
}
