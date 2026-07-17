namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Kernel.Results;

/// <summary>
/// Handler do <see cref="DefinirReferenciaTemporalFatosCommand"/> (Story #554, PR #896):
/// <c>Tipo</c> nulo remove a referência (toggle por ausência); caso contrário monta o VO
/// e delega a validação estrutural (coerência por variante, fase do próprio processo)
/// ao domínio.
/// </summary>
public static class DefinirReferenciaTemporalFatosCommandHandler
{
    public static async Task<Result<MutacaoAceita>> Handle(
        DefinirReferenciaTemporalFatosCommand command,
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

        ReferenciaTemporalFatos? referencia = null;
        if (command.Tipo is not null)
        {
            ReferenciaTipo tipo = ReferenciaTipoCodigo.FromCodigo(command.Tipo);
            Result<ReferenciaTemporalFatos> referenciaResult = ReferenciaTemporalFatos.Criar(tipo, command.Data, command.FaseId);
            if (referenciaResult.IsFailure)
            {
                return Result<MutacaoAceita>.Failure(referenciaResult.Error!);
            }

            referencia = referenciaResult.Value!;
        }

        Result definirResult = processo.DefinirReferenciaTemporalFatos(referencia, command.Precondicao);
        if (definirResult.IsFailure)
        {
            return Result<MutacaoAceita>.Failure(definirResult.Error!);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<MutacaoAceita>.Success(new MutacaoAceita(processo.ETagDaSessaoEditorial));
    }
}
