namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using DTOs;

using Kernel.Results;

using Services.Rascunhos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

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
        DiagnosticoDoRascunho diagnostico,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(rascunhos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(diagnostico);

        Result<NovoRascunhoConferido> novo = await NovoRascunhoAsync(command, preparacao, unidades, diagnostico, cancellationToken).ConfigureAwait(false);
        if (novo.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(novo.Errors);
        }

        return await GravarAsync(novo.Value!, rascunhos, unitOfWork, diagnostico, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<NovoRascunhoConferido>> NovoRascunhoAsync(
        CriarRascunhoDeProcessoSeletivoCommand command,
        PreparacaoDoDocumentoDoRascunho preparacao,
        UnidadeDoDocumentoDoRascunho unidades,
        DiagnosticoDoRascunho diagnostico,
        CancellationToken cancellationToken)
    {
        Result<DocumentoPreparado> preparado = preparacao.Preparar(command.Documento);
        if (preparado.IsFailure)
        {
            return Result<NovoRascunhoConferido>.ValidationFailure(preparado.Errors);
        }

        Result<UnidadeView> unidade = await unidades.ResolverEExigirAsync(preparado.Value!.CodigoDaUnidade, cancellationToken).ConfigureAwait(false);
        return unidade.IsFailure
            ? Result<NovoRascunhoConferido>.ValidationFailure(unidade.Errors)
            : await CriarEConferirAsync(unidade.Value!.Id, preparado.Value.Documento, diagnostico, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<NovoRascunhoConferido>> CriarEConferirAsync(
        Guid unidadeId, DocumentoDoRascunho documento, DiagnosticoDoRascunho diagnostico, CancellationToken cancellationToken)
    {
        Result<RascunhoDeProcessoSeletivo> rascunho = RascunhoDeProcessoSeletivo.Criar(unidadeId, documento);
        if (rascunho.IsFailure)
        {
            return Result<NovoRascunhoConferido>.ValidationFailure(rascunho.Errors);
        }

        DocumentoConferidoAntesDeGravar conferido = await diagnostico.ConferirAntesDeGravarAsync(documento, cancellationToken).ConfigureAwait(false);
        return Result<NovoRascunhoConferido>.Success(new NovoRascunhoConferido(rascunho.Value!, conferido));
    }

    private static async Task<Result<RascunhoSalvoDto>> GravarAsync(
        NovoRascunhoConferido novo, IRascunhoDeProcessoSeletivoRepository rascunhos,
        IUnitOfWork unitOfWork, DiagnosticoDoRascunho diagnostico, CancellationToken cancellationToken)
    {
        RascunhoDeProcessoSeletivo rascunho = novo.Rascunho;
        await rascunhos.AdicionarAsync(rascunho, cancellationToken).ConfigureAwait(false);
        Result gravacao = await SalvarRecusandoConflitoAsync(unitOfWork, cancellationToken).ConfigureAwait(false);
        if (gravacao.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(gravacao.Errors);
        }

        IReadOnlyList<FieldError> diagnosticos = await diagnostico.DiagnosticarAsync(rascunho, novo.Conferido, cancellationToken).ConfigureAwait(false);
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

    /// <summary>O rascunho novo e o que o documento dele já tem conferido, para o diagnóstico depois de gravar.</summary>
    private sealed record NovoRascunhoConferido(RascunhoDeProcessoSeletivo Rascunho, DocumentoConferidoAntesDeGravar Conferido);
}
