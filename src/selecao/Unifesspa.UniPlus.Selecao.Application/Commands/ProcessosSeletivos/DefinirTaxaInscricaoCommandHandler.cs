namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;


/// <summary>
/// Handler do <see cref="DefinirTaxaInscricaoCommand"/>: constrói <see cref="ConfiguracaoTaxaInscricao"/> via
/// <see cref="ConfiguracaoTaxaInscricao.Criar"/>, que exige <c>Cobra</c> e aplica as regras de valor, de
/// exclusividade com a isenção e de fundamento obrigatório.
/// </summary>
public static class DefinirTaxaInscricaoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirTaxaInscricaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        return obtido.IsFailure
            ? Result.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirTaxaInscricaoCommand command,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result definido = Definir(processo, command);
        if (definido.IsFailure)
        {
            return definido;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static Result Definir(ProcessoSeletivo processo, DefinirTaxaInscricaoCommand command)
    {
        Result<ConfiguracaoTaxaInscricao> declarada = ConfiguracaoTaxaInscricao.Criar(command.Cobra, command.Valor, command.Fundamentos);
        if (declarada.IsFailure)
        {
            return Result.ValidationFailure(declarada.Errors);
        }

        Result definido = processo.DefinirTaxaInscricao(declarada.Value);
        return definido.IsFailure ? Result.Failure(definido.Error!) : definido;
    }
}
