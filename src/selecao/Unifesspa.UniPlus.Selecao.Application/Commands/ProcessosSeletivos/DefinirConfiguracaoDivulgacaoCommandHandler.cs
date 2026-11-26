namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Services;

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

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao.ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        Result definido = obtido.IsFailure ? Result.Failure(obtido.Error!) : Definir(obtido.Value!, command);
        if (definido.IsSuccess)
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return definido;
    }

    private static Result Definir(ProcessoSeletivo processo, DefinirConfiguracaoDivulgacaoCommand command)
    {
        if (command.CamposPublicos is null)
        {
            return processo.DefinirConfiguracaoDivulgacao(null);
        }

        Result<ConfiguracaoDivulgacao> criada = ConfiguracaoDivulgacao.Criar(command.CamposPublicos, command.Justificativa);
        return criada.IsFailure
            ? Result.ValidationFailure(criada.Errors)
            : processo.DefinirConfiguracaoDivulgacao(criada.Value!);
    }
}
