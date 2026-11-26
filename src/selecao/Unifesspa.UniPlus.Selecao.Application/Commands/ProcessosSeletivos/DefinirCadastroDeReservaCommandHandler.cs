namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public static class DefinirCadastroDeReservaCommandHandler
{
    public static async Task<Result> Handle(
        DefinirCadastroDeReservaCommand command, IProcessoSeletivoRepository repositorio,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(repositorio, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirAsync(obtido.Value!, command, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo, DefinirCadastroDeReservaCommand command, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result resultado = processo.DefinirCadastroDeReserva(command.Chamadas);
        return resultado.IsFailure
            ? Result.ValidationFailure(resultado.Errors)
            : await DefinirProvaPresencialCommandHandler.SalvarAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
    }
}
