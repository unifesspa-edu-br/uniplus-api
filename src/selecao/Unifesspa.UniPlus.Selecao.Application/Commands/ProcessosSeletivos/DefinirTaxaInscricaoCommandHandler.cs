namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirTaxaInscricaoCommand"/> (issue #1112): <c>Cobra</c> nulo remove
/// a declaração (toggle por ausência, mas ausência aqui bloqueia publicação — CA-01); caso
/// contrário constrói <see cref="ConfiguracaoTaxaInscricao"/> via <see cref="ConfiguracaoTaxaInscricao.Criar"/>,
/// que aplica as regras de valor, de exclusividade com a isenção e de fundamento obrigatório.
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

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        // Fora do rascunho a recusa vem antes das regras de negócio.
        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        if (command.Cobra is null)
        {
            Result removerResult = processo.DefinirTaxaInscricao(null);
            if (removerResult.IsFailure)
            {
                return Result.Failure(removerResult.Error!);
            }

            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        Result<ConfiguracaoTaxaInscricao> configuracaoResult = ConfiguracaoTaxaInscricao.Criar(
            command.Cobra.Value, command.Valor, command.Fundamentos);
        if (configuracaoResult.IsFailure)
        {
            return Result.ValidationFailure(configuracaoResult.Errors);
        }

        Result definirResult = processo.DefinirTaxaInscricao(configuracaoResult.Value!);
        if (definirResult.IsFailure)
        {
            return Result.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
