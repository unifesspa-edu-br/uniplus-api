namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirCronogramaFasesCommand"/>: trava o processo para edição, delega a definição do
/// cronograma (ver <see cref="DefinicaoDoCronogramaDeFases"/>) com o relógio injetado lido <b>uma vez</b> por
/// operação (ADR-0068) e grava. A recusa do domínio sai como LOTE: a conclusão do ciclo recursal acumula uma
/// violação por fase mal declarada, e reduzi-la a um <c>DomainError</c> apagaria os campos que localizam cada uma.
/// </summary>
public static class DefinirCronogramaFasesCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirCronogramaFasesCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        DefinicaoDoCronogramaDeFases definicao,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Result<ProcessoSeletivo> obtido = await ObterParaEdicaoAsync(processoSeletivoRepository, command, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure
            ? Result<MutacaoAceita>.Failure(obtido.Error!)
            : await DefinirEGravarAsync(obtido.Value!, command, definicao, unitOfWork, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    // A precondição precede a resolução de regras externas (ADR-0110).
    private static async Task<Result<ProcessoSeletivo>> ObterParaEdicaoAsync(
        IProcessoSeletivoRepository repositorio, DefinirCronogramaFasesCommand command, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        return processo.MutacaoBloqueada(command.Precondicao) is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }

    private static async Task<Result<MutacaoAceita>> DefinirEGravarAsync(
        ProcessoSeletivo processo,
        DefinirCronogramaFasesCommand command,
        DefinicaoDoCronogramaDeFases definicao,
        ISelecaoUnitOfWork unitOfWork,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        Result definida = await definicao.DefinirAsync(processo, command, agora, cancellationToken).ConfigureAwait(false);
        if (definida.IsFailure)
        {
            return Result<MutacaoAceita>.ValidationFailure(definida.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
