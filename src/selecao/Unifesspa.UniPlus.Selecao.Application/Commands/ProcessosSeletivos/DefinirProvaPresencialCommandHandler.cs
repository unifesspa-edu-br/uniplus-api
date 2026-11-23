namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Kernel.Results;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public static class DefinirProvaPresencialCommandHandler
{
    public static async Task<Result> Handle(
        DefinirProvaPresencialCommand command, IProcessoSeletivoRepository repositorio,
        IUnitOfWork unitOfWork, IOptions<ProvaPresencialOptions> options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(options);
        return await DefinirAsync(command, repositorio, unitOfWork, options.Value, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        DefinirProvaPresencialCommand command, IProcessoSeletivoRepository repositorio,
        IUnitOfWork unitOfWork, ProvaPresencialOptions options, CancellationToken cancellationToken)
    {
        Result<ProcessoSeletivo> obtido = await DefinirBonusRegionalCommandHandler
            .ObterParaEdicaoAsync(repositorio, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (obtido.IsFailure)
        {
            return Result.Failure(obtido.Error!);
        }

        Result resultado = obtido.Value!.DefinirProvaPresencial(command.TemProvaPresencial, command.Municipios, options.LimiteDeMunicipios);
        return resultado.IsFailure ? Result.ValidationFailure(resultado.Errors) : await SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> SalvarAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia,
                "O processo foi alterado concorrentemente. Tente novamente."));
        }
    }
}
