namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// Obtém o processo seletivo para uma operação que altera a configuração, com trava de escrita. Quando o processo não
/// existe ou já não está em rascunho, devolve a recusa em vez do processo.
/// </summary>
internal static class ProcessoSeletivoEmEdicao
{
    internal static async Task<Result<ProcessoSeletivo>> ObterAsync(
        IProcessoSeletivoRepository repositorio, Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        ProcessoSeletivo? processo = await repositorio.ObterParaMutacaoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (processo is null)
        {
            return Result<ProcessoSeletivo>.Failure(DefinirFormularioCommandHandler.ProcessoNaoEncontrado(processoSeletivoId));
        }

        return processo.MutacaoBloqueada() is { } bloqueio
            ? Result<ProcessoSeletivo>.Failure(bloqueio)
            : Result<ProcessoSeletivo>.Success(processo);
    }
}
