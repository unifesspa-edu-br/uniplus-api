namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirCascataRemanejamentoCommand"/>
/// (cascata de remanejamento de cotas, UNI-REQ-0056, Story #575): campos todos nulos remove a cascata
/// (<see cref="ProcessoSeletivo.DefinirCascataRemanejamento"/> com
/// <see langword="null"/>); todos presentes valida a forma de cada destino e a
/// coerência entre eles (cascata de remanejamento de cotas, UNI-REQ-0056) ANTES de consultar o <c>regras_do_catalogo</c>
/// (ADR-0125: validação sempre precede I/O) e só então compara célula a célula
/// contra o <c>declaracao_da_regra</c> congelado da regra resolvida (conteúdo —
/// vínculo com a regra versionada da cascata de remanejamento de cotas, UNI-REQ-0056) — ver §4 da story para o porquê da ordem entre forma e conteúdo.
/// </summary>
public static class DefinirCascataRemanejamentoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirCascataRemanejamentoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegraCatalogoReader regraCatalogoReader,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(regraCatalogoReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<ProcessoSeletivo> obtido = await ProcessoSeletivoEmEdicao
            .ObterAsync(processoSeletivoRepository, command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return obtido.IsFailure ? Result.Failure(obtido.Error!)
            : await AplicarAsync(command, obtido.Value!, regraCatalogoReader, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> AplicarAsync(DefinirCascataRemanejamentoCommand command,
        ProcessoSeletivo processo, IRegraCatalogoReader regraCatalogoReader, IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        Result<ConfiguracaoCascataRemanejamento?> resolucao = await ResolucaoDaCascataRemanejamento
            .ResolverAsync(command, regraCatalogoReader, cancellationToken).ConfigureAwait(false);
        if (resolucao.IsFailure)
        {
            return Result.ValidationFailure(resolucao.Errors);
        }

        return await GravarAsync(processo, resolucao.Value, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> GravarAsync(ProcessoSeletivo processo, ConfiguracaoCascataRemanejamento? cascata,
        IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        Result definirResult = processo.DefinirCascataRemanejamento(cascata);
        if (definirResult.IsFailure)
        {
            return Result.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
