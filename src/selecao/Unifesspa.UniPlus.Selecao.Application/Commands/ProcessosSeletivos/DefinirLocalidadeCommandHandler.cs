namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;


/// <summary>
/// Handler do <see cref="DefinirLocalidadeCommand"/> (UNI-REQ-0111): monta o value object
/// a partir do que foi declarado e delega ao domínio. A recusa por trio não declarado sai
/// aqui com o código que o catálogo público publica para a causa, em vez de virar erro
/// genérico de validação de contrato — é ele que leva o consumidor à explicação.
/// </summary>
public static class DefinirLocalidadeCommandHandler
{
    public static async Task<Result> Handle(
        DefinirLocalidadeCommand command,
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
        DefinirLocalidadeCommand command,
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

    private static Result Definir(ProcessoSeletivo processo, DefinirLocalidadeCommand command)
    {
        if (command.LocalidadeNaoDeclarada)
        {
            return Result.Failure(LocalidadeAusente());
        }

        Result<LocalidadeRegente> declarada = LocalidadeRegente.Criar(command.CodigoIbge, command.Nome, command.Uf);
        if (declarada.IsFailure)
        {
            return Result.ValidationFailure(declarada.Errors);
        }

        Result definido = processo.DefinirLocalidade(declarada.Value!);
        return definido.IsFailure ? Result.Failure(definido.Error!) : definido;
    }

    private static DomainError LocalidadeAusente() => new(
        "ProcessoSeletivo.LocalidadeAusente",
        "A localidade que rege a contagem dos prazos é obrigatória para alterar o processo seletivo.");
}
