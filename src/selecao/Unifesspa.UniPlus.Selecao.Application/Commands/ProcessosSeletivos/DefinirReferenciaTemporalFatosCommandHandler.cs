namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;


/// <summary>
/// Handler do <see cref="DefinirReferenciaTemporalFatosCommand"/> (Story #554, PR #896):
/// <c>Tipo</c> nulo remove a referência temporal dos dados do candidato (toggle por ausência);
/// caso contrário monta o value object e delega a validação estrutural (coerência por
/// variante, fase do próprio processo) ao domínio.
/// </summary>
public static class DefinirReferenciaTemporalFatosCommandHandler
{
    public static async Task<Result> Handle(
        DefinirReferenciaTemporalFatosCommand command,
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
        DefinirReferenciaTemporalFatosCommand command,
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

    private static Result Definir(ProcessoSeletivo processo, DefinirReferenciaTemporalFatosCommand command)
    {
        Result<ReferenciaTemporalFatos>? declarada = command.Tipo is null
            ? null
            : ReferenciaTemporalFatos.Criar(command.Tipo, command.Data, command.FaseId);
        if (declarada is { IsFailure: true })
        {
            return Result.ValidationFailure(declarada.Errors);
        }

        return processo.DefinirReferenciaTemporalFatos(declarada?.Value);
    }
}
