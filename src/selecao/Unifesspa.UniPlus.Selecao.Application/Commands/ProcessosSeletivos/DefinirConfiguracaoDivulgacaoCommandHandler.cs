namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirConfiguracaoDivulgacaoCommand"/> (UNI-REQ-0050, issue #563):
/// <c>CamposPublicos</c> nulo remove a configuração explícita (toggle por ausência, mesmo
/// padrão de <see cref="DefinirBonusRegionalCommandHandler"/>); caso contrário reconstrói a
/// entidade por inteiro.
/// </summary>
public static class DefinirConfiguracaoDivulgacaoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirConfiguracaoDivulgacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ISelecaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<MutacaoAceita>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        ConfiguracaoDivulgacao? configuracao = null;
        if (command.CamposPublicos is not null)
        {
            Result<ConfiguracaoDivulgacao> configuracaoResult =
                ConfiguracaoDivulgacao.Criar(command.CamposPublicos, command.Justificativa);
            if (configuracaoResult.IsFailure)
            {
                return Result<MutacaoAceita>.ValidationFailure(configuracaoResult.Errors);
            }

            configuracao = configuracaoResult.Value!;
        }

        Result definirResult = processo.DefinirConfiguracaoDivulgacao(configuracao, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
