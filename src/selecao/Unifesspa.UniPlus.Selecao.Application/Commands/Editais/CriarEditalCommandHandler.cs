namespace Unifesspa.UniPlus.Selecao.Application.Commands.Editais;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Kernel.Results;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

/// <summary>
/// Handler convention-based do <see cref="CriarEditalCommand"/>: cria o
/// agregado <see cref="Edital"/> a partir do <see cref="NumeroEdital"/>
/// validado, persiste via repositório e retorna o id no <see cref="Result{T}"/>.
/// Validação do request fica fora deste método — responsabilidade do
/// middleware de validação FluentValidation do Wolverine (<c>UseFluentValidation</c>).
/// </summary>
public static class CriarEditalCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarEditalCommand command,
        IEditalRepository editalRepository,
        ISelecaoUnitOfWork unitOfWork,
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

        Edital edital = Edital.Criar(numeroResult.Value!, command.Titulo, command.TipoEditalId);

        await editalRepository.AdicionarAsync(edital, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(edital.Id);
    }
}
