namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using DTOs;

using Kernel.Results;

using Services.Rascunhos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="CriarRascunhoDeProcessoSeletivoCommand"/>: a forma do documento, a unidade que ele nomeia e a
/// permissão de configurar nela, nessa ordem, antes de guardar. O POST tem chave de idempotência, então o conflito de
/// concorrência é devolvido como recusa depois de descartar o rastreamento (ADR-0119).
/// </summary>
public static class CriarRascunhoDeProcessoSeletivoCommandHandler
{
    public static async Task<Result<RascunhoSalvoDto>> Handle(
        CriarRascunhoDeProcessoSeletivoCommand command,
        PreparacaoDoDocumentoDoRascunho preparacao,
        UnidadeDoDocumentoDoRascunho unidades,
        IRascunhoDeProcessoSeletivoRepository rascunhos,
        IUnitOfWork unitOfWork,
        ResolucaoDosModelosCitados resolucao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(rascunhos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(resolucao);

        Result<RascunhoDeProcessoSeletivo> rascunho = await NovoRascunhoAsync(command, preparacao, unidades, cancellationToken).ConfigureAwait(false);
        if (rascunho.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(rascunho.Errors);
        }

        return await GravarAsync(rascunho.Value!, rascunhos, unitOfWork, resolucao, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<RascunhoDeProcessoSeletivo>> NovoRascunhoAsync(
        CriarRascunhoDeProcessoSeletivoCommand command,
        PreparacaoDoDocumentoDoRascunho preparacao,
        UnidadeDoDocumentoDoRascunho unidades,
        CancellationToken cancellationToken)
    {
        Result<DocumentoPreparado> preparado = preparacao.Preparar(command.Documento);
        if (preparado.IsFailure)
        {
            return Result<RascunhoDeProcessoSeletivo>.ValidationFailure(preparado.Errors);
        }

        Result<UnidadeView> unidade = await unidades.ResolverEExigirAsync(preparado.Value!.CodigoDaUnidade, cancellationToken).ConfigureAwait(false);
        return unidade.IsFailure
            ? Result<RascunhoDeProcessoSeletivo>.ValidationFailure(unidade.Errors)
            : RascunhoDeProcessoSeletivo.Criar(unidade.Value!.Id, preparado.Value.Documento);
    }

    private static async Task<Result<RascunhoSalvoDto>> GravarAsync(
        RascunhoDeProcessoSeletivo rascunho, IRascunhoDeProcessoSeletivoRepository rascunhos,
        IUnitOfWork unitOfWork, ResolucaoDosModelosCitados resolucao, CancellationToken cancellationToken)
    {
        await rascunhos.AdicionarAsync(rascunho, cancellationToken).ConfigureAwait(false);
        Result gravacao = await SalvarRecusandoConflitoAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
        if (gravacao.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(gravacao.Errors);
        }

        IReadOnlyList<FieldError> diagnosticos = await resolucao.DiagnosticarAsync(rascunho, cancellationToken).ConfigureAwait(false);
        return Result<RascunhoSalvoDto>.Success(RascunhoSalvoDto.DoRascunho(rascunho, diagnosticos));
    }

    private static async Task<Result> SalvarRecusandoConflitoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia,
                "O rascunho foi alterado concorrentemente. Tente novamente."));
        }
    }
}
