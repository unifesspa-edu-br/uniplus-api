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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(rascunhos);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<RascunhoDeProcessoSeletivo> rascunho = await NovoRascunhoAsync(command, preparacao, unidades, cancellationToken).ConfigureAwait(false);
        if (rascunho.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(rascunho.Errors);
        }

        await rascunhos.AdicionarAsync(rascunho.Value!, cancellationToken).ConfigureAwait(false);
        return await GravarAsync(rascunho.Value!, unitOfWork, cancellationToken).ConfigureAwait(false);
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
        RascunhoDeProcessoSeletivo rascunho, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (OptimisticConcurrencyViolation.Is(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<RascunhoSalvoDto>.Failure(new DomainError(
                ProcessoSeletivoErrorCodes.ConflitoDeConcorrencia,
                "O rascunho foi alterado concorrentemente. Tente novamente."));
        }

        return Result<RascunhoSalvoDto>.Success(new RascunhoSalvoDto(rascunho.Id, rascunho.Revisao, [], rascunho.ETag));
    }
}
