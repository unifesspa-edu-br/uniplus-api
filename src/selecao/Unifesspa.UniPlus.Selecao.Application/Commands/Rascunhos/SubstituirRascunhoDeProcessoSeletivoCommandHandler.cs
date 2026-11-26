namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using DTOs;

using Kernel.Results;

using Services.Rascunhos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Handler do <see cref="SubstituirRascunhoDeProcessoSeletivoCommand"/>. A ordem das recusas é a do contrato: precondição
/// ausente, forma, unidade ausente, e só então o bloqueio da linha, onde ficam a existência, a revisão e o acesso à unidade nova.
/// A conferência no Geo vem antes do bloqueio, porque a espera por ele não pode ficar presa à trava da linha, e depois da permissão
/// sobre o processo, decidida antes de o corpo ser lido. A permissão na unidade nova fica sob a trava, depois da revisão: a recusa
/// dela pode vir depois de o Geo ser consultado, no limite de municípios configurado. Sem chave de idempotência e sob <c>FOR UPDATE</c>, a exceção de concorrência não é esperada e propaga ao tratamento central.
/// </summary>
public static class SubstituirRascunhoDeProcessoSeletivoCommandHandler
{
    public static async Task<Result<RascunhoSalvoDto>> Handle(
        SubstituirRascunhoDeProcessoSeletivoCommand command,
        PreparacaoDoDocumentoDoRascunho preparacao,
        UnidadeDoDocumentoDoRascunho unidades,
        BloqueioDoRascunhoDeProcessoSeletivo bloqueio,
        IUnitOfWork unitOfWork,
        DiagnosticoDoRascunho diagnostico,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(unidades);
        ArgumentNullException.ThrowIfNull(bloqueio);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(diagnostico);

        Result<SubstituicaoPreparada> preparada = await PrepararAsync(command, preparacao, unidades, bloqueio, diagnostico, cancellationToken).ConfigureAwait(false);
        if (preparada.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(preparada.Errors);
        }

        return await SubstituirAsync(preparada.Value!, command.Precondicao, unidades, unitOfWork, diagnostico, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<SubstituicaoPreparada>> PrepararAsync(
        SubstituirRascunhoDeProcessoSeletivoCommand command,
        PreparacaoDoDocumentoDoRascunho preparacao,
        UnidadeDoDocumentoDoRascunho unidades,
        BloqueioDoRascunhoDeProcessoSeletivo bloqueio,
        DiagnosticoDoRascunho diagnostico,
        CancellationToken cancellationToken)
    {
        Result<DocumentoPreparado> preparado = Preparar(command, preparacao);
        if (preparado.IsFailure)
        {
            return Result<SubstituicaoPreparada>.ValidationFailure(preparado.Errors);
        }

        UnidadeView? nova = await unidades.ResolverAsync(preparado.Value!.CodigoDaUnidade, cancellationToken).ConfigureAwait(false);
        DocumentoConferidoAntesDeGravar conferido = await diagnostico.ConferirAntesDeGravarAsync(preparado.Value.Documento, cancellationToken).ConfigureAwait(false);
        Result<RascunhoDeProcessoSeletivo> rascunho = await bloqueio.BloquearEConferirAsync(command.Id, command.Precondicao, cancellationToken).ConfigureAwait(false);
        return rascunho.IsFailure
            ? Result<SubstituicaoPreparada>.ValidationFailure(rascunho.Errors)
            : Result<SubstituicaoPreparada>.Success(new SubstituicaoPreparada(rascunho.Value!, preparado.Value.Documento, nova, conferido));
    }

    // A precondição ausente é a primeira recusa: nada do documento se lê antes dela.
    private static Result<DocumentoPreparado> Preparar(SubstituirRascunhoDeProcessoSeletivoCommand command, PreparacaoDoDocumentoDoRascunho preparacao)
    {
        Result exigida = RascunhoDeProcessoSeletivo.ExigirPrecondicao(command.Precondicao);
        return exigida.IsFailure
            ? Result<DocumentoPreparado>.ValidationFailure(exigida.Errors)
            : preparacao.Preparar(command.Documento);
    }

    private static async Task<Result<RascunhoSalvoDto>> SubstituirAsync(
        SubstituicaoPreparada preparada,
        PrecondicaoIfMatch precondicao,
        UnidadeDoDocumentoDoRascunho unidades,
        IUnitOfWork unitOfWork,
        DiagnosticoDoRascunho diagnostico,
        CancellationToken cancellationToken)
    {
        RascunhoDeProcessoSeletivo rascunho = preparada.Rascunho;
        Result<Guid> unidade = await unidades.ExigirParaTrocaAsync(preparada.UnidadeNova, rascunho.UnidadeAdministradoraOrigemId, cancellationToken).ConfigureAwait(false);
        Result substituicao = unidade.IsFailure ? Result.ValidationFailure(unidade.Errors) : rascunho.Substituir(preparada.Documento, unidade.Value, precondicao);
        if (substituicao.IsFailure)
        {
            return Result<RascunhoSalvoDto>.ValidationFailure(substituicao.Errors);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> diagnosticos = await diagnostico.DiagnosticarAsync(rascunho, preparada.Conferido, cancellationToken).ConfigureAwait(false);
        return Result<RascunhoSalvoDto>.Success(RascunhoSalvoDto.DoRascunho(rascunho, diagnosticos));
    }

    /// <summary>
    /// O que a substituição leva até a gravação: o rascunho bloqueado e conferido, o documento novo, a unidade que ele nomeia e o que
    /// o documento tem conferido no Geo.
    /// </summary>
    private sealed record SubstituicaoPreparada(
        RascunhoDeProcessoSeletivo Rascunho, DocumentoDoRascunho Documento, UnidadeView? UnidadeNova, DocumentoConferidoAntesDeGravar Conferido);
}
