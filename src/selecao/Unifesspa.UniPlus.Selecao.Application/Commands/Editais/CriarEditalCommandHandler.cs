namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using MediatR;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class CriarEditalCommandHandler : IRequestHandler<CriarEditalCommand, Result<Guid>>
{
    private readonly IEditalRepository _editalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CriarEditalCommandHandler(IEditalRepository editalRepository, IUnitOfWork unitOfWork)
    {
        _editalRepository = editalRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CriarEditalCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(request.NumeroEdital, request.AnoEdital);
        if (numeroResult.IsFailure)
            return Result<Guid>.Failure(numeroResult.Error!);

        Edital edital = Edital.Criar(numeroResult.Value!, request.Titulo, request.TipoProcesso);

        await _editalRepository.AdicionarAsync(edital, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(edital.Id);
    }
}
