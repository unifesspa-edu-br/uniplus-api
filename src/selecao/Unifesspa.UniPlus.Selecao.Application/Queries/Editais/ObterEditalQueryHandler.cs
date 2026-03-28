namespace Unifesspa.UniPlus.Selecao.Application.Queries.Editais;

using MediatR;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed class ObterEditalQueryHandler : IRequestHandler<ObterEditalQuery, Result<EditalDto>>
{
    private readonly IEditalRepository _editalRepository;

    public ObterEditalQueryHandler(IEditalRepository editalRepository)
    {
        _editalRepository = editalRepository;
    }

    public async Task<Result<EditalDto>> Handle(ObterEditalQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Edital? edital = await _editalRepository.ObterPorIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (edital is null)
            return Result<EditalDto>.Failure(new DomainError("Edital.NaoEncontrado", "Edital não encontrado."));

        EditalDto dto = new(
            edital.Id,
            edital.NumeroEdital.ToString(),
            edital.Titulo,
            edital.TipoProcesso.ToString(),
            edital.Status.ToString(),
            edital.MaximoOpcoesCurso,
            edital.BonusRegionalHabilitado,
            edital.CreatedAt);

        return Result<EditalDto>.Success(dto);
    }
}
