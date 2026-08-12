namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirTaxaInscricaoCommand"/> (issue #1112): <c>Cobra</c> nulo remove
/// a declaração (toggle por ausência, mas ausência aqui bloqueia publicação — CA-01); caso
/// contrário constrói <see cref="ConfiguracaoTaxaInscricao"/> via <see cref="ConfiguracaoTaxaInscricao.Criar"/>,
/// que aplica CA-02/CA-03/CA-06.
/// </summary>
public static class DefinirTaxaInscricaoCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirTaxaInscricaoCommand command,
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

        // A precondição é conferida AQUI, logo depois do 404 e antes das regras de negócio que
        // este handler avalia — mesma ordem da ADR-0110 D9 já aplicada em
        // DefinirBonusRegionalCommandHandler.
        if (processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio)
        {
            return Result<MutacaoAceita>.Failure(bloqueio);
        }

        if (command.Cobra is null)
        {
            Result removerResult = processo.DefinirTaxaInscricao(null, command.Precondicao);
            if (removerResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(removerResult.Error!);
            }

            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
        }

        Result<ConfiguracaoTaxaInscricao> configuracaoResult = ConfiguracaoTaxaInscricao.Criar(
            command.Cobra.Value, command.Valor, command.Fundamentos, command.ConfirmacaoFundamentos);
        if (configuracaoResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(configuracaoResult.Error!);
        }

        Result definirResult = processo.DefinirTaxaInscricao(configuracaoResult.Value!, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
