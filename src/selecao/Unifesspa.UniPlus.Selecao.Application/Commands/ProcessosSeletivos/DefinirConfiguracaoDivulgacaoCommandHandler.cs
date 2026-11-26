namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirConfiguracaoDivulgacaoCommand"/> (UNI-REQ-0050, issue #563):
/// <c>CamposPublicos</c> nulo remove a configuração explícita (toggle por ausência, mesmo
/// padrão de <see cref="DefinirFadaCommandHandler"/>); caso contrário reconstrói a
/// entidade por inteiro.
/// </summary>
public static class DefinirConfiguracaoDivulgacaoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirConfiguracaoDivulgacaoCommand command,
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
        DefinirConfiguracaoDivulgacaoCommand command,
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

    private static Result Definir(ProcessoSeletivo processo, DefinirConfiguracaoDivulgacaoCommand command)
    {
        Result<ConfiguracaoDivulgacao>? declarada = command.CamposPublicos is null
            ? null
            : ConfiguracaoDivulgacao.Criar(command.CamposPublicos, command.Justificativa);
        if (declarada is { IsFailure: true })
        {
            return Result.ValidationFailure(declarada.Errors);
        }

        Result definido = processo.DefinirConfiguracaoDivulgacao(declarada?.Value);
        return definido.IsFailure ? Result.Failure(definido.Error!) : definido;
    }
}
