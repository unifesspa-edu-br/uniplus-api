namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler convention-based do <see cref="CriarEditalCommand"/>: cria o
/// agregado <see cref="Edital"/> a partir do <see cref="NumeroEdital"/>
/// validado, persiste via repositório e retorna o id no <see cref="Result{T}"/>.
/// Validação do request fica fora deste método — responsabilidade do
/// <c>WolverineValidationMiddleware</c>.
/// </summary>
public static class CriarEditalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarEditalCommand command,
        IEditalRepository editalRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(editalRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<NumeroEdital> numeroResult = NumeroEdital.Criar(command.NumeroEdital, command.AnoEdital);
        if (numeroResult.IsFailure)
        {
            return Result<Guid>.Failure(numeroResult.Error!);
        }

        Edital edital = Edital.Criar(numeroResult.Value!, command.Titulo, command.TipoProcesso);

        await editalRepository.AdicionarAsync(edital, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(edital.Id);
    }
}
