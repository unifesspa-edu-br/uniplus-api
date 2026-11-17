namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A entrada de toda mutação de um rascunho existente: exige a precondição, bloqueia a linha, confere que ela existe, que a
/// pessoa configura na unidade que ela tem agora e que o <c>If-Match</c> casa com a revisão que o bloqueio devolveu. Quem muta
/// o rascunho o recebe já conferido, sob o bloqueio.
/// </summary>
public sealed class BloqueioDoRascunhoDeProcessoSeletivo(
    IRascunhoDeProcessoSeletivoRepository rascunhos,
    IProcessoSeletivoRepository processos,
    IVerificadorDeAcesso verificador)
{
    public async Task<Result<RascunhoDeProcessoSeletivo>> BloquearEConferirAsync(
        Guid id, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        Result exigida = RascunhoDeProcessoSeletivo.ExigirPrecondicao(precondicao);
        if (exigida.IsFailure)
        {
            return Result<RascunhoDeProcessoSeletivo>.ValidationFailure(exigida.Errors);
        }

        RascunhoDeProcessoSeletivo? rascunho = await rascunhos.ObterParaMutacaoAsync(id, cancellationToken).ConfigureAwait(false);
        return rascunho is null
            ? Result<RascunhoDeProcessoSeletivo>.Failure(await RecusaDaLinhaAusenteAsync(id, cancellationToken).ConfigureAwait(false))
            : await ConferirAcessoERevisaoAsync(rascunho, precondicao, cancellationToken).ConfigureAwait(false);
    }

    // O acesso vem antes da revisão: quem não alcança a unidade atual não aprende nada da linha, nem pelo 412.
    private async Task<Result<RascunhoDeProcessoSeletivo>> ConferirAcessoERevisaoAsync(
        RascunhoDeProcessoSeletivo rascunho, PrecondicaoIfMatch precondicao, CancellationToken cancellationToken)
    {
        Result acesso = await AcessoAoProcesso
            .ExigirConfigurarNaUnidadeAtualAsync(verificador, rascunho.UnidadeAdministradoraOrigemId, rascunho.Id, cancellationToken)
            .ConfigureAwait(false);

        return acesso.IsFailure
            ? Result<RascunhoDeProcessoSeletivo>.Failure(acesso.Error!)
            : ConferirRevisao(rascunho, precondicao);
    }

    private static Result<RascunhoDeProcessoSeletivo> ConferirRevisao(RascunhoDeProcessoSeletivo rascunho, PrecondicaoIfMatch precondicao)
    {
        Result conferida = rascunho.ConferirPrecondicao(precondicao);
        return conferida.IsFailure
            ? Result<RascunhoDeProcessoSeletivo>.ValidationFailure(conferida.Errors)
            : Result<RascunhoDeProcessoSeletivo>.Success(rascunho);
    }

    // O rascunho some quando a publicação o consome: se o processo de mesmo id existe, o recurso virou processo (409); senão, nunca existiu.
    private async Task<DomainError> RecusaDaLinhaAusenteAsync(Guid id, CancellationToken cancellationToken) =>
        await processos.ExisteAsync(id, cancellationToken).ConfigureAwait(false)
            ? new DomainError(
                RascunhoDeProcessoSeletivoErrorCodes.JaPublicado,
                "O rascunho já foi publicado e virou o processo seletivo de mesmo identificador; consulte o processo publicado.")
            : AcessoAoProcesso.NaoEncontrado(id);
}
