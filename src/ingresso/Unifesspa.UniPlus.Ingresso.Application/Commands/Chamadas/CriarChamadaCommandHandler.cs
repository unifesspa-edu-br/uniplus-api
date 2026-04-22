namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Chamadas;

using MediatR;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Ingresso.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarChamadaCommandHandler : IRequestHandler<CriarChamadaCommand, Result<Guid>>
{
    private readonly IChamadaRepository _chamadaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarChamadaCommandHandler(IChamadaRepository chamadaRepository, IUnitOfWork unitOfWork)
    {
        _chamadaRepository = chamadaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CriarChamadaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PrazoManifestacao <= request.DataPublicacao)
            return Result<Guid>.Failure(new DomainError("Chamada.PrazoInvalido", "Prazo de manifestação deve ser posterior à data de publicação."));

        int proximoNumero = await _chamadaRepository
            .ObterProximoNumeroChamadaAsync(request.EditalId, cancellationToken)
            .ConfigureAwait(false);

        Chamada chamada = Chamada.Criar(request.EditalId, proximoNumero, request.DataPublicacao, request.PrazoManifestacao);

        await _chamadaRepository.AdicionarAsync(chamada, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(chamada.Id);
    }
}
