namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>
/// Handler do <see cref="DefinirReferenciaTemporalFatosCommand"/> (Story #554, PR #896):
/// <c>Tipo</c> nulo remove a referência (toggle por ausência); caso contrário monta o VO
/// e delega a validação estrutural (coerência por variante, fase do próprio processo)
/// ao domínio.
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

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterParaMutacaoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return Result.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {command.ProcessoSeletivoId} não encontrado."));
        }

        if (processo.MutacaoBloqueada() is { } bloqueio)
        {
            return Result.Failure(bloqueio);
        }

        ReferenciaTemporalFatos? referencia = null;
        if (command.Tipo is not null)
        {
            Result<ReferenciaTemporalFatos> referenciaResult = ReferenciaTemporalFatos.Criar(command.Tipo, command.Data, command.FaseId);
            if (referenciaResult.IsFailure)
            {
                return Result.ValidationFailure(referenciaResult.Errors);
            }

            referencia = referenciaResult.Value!;
        }

        Result definirResult = processo.DefinirReferenciaTemporalFatos(referencia);
        if (definirResult.IsFailure)
        {
            return Result.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
