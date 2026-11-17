namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Reclassificacoes;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O detalhe da reclassificação.</summary>
public static class ObterReclassificacaoQueryHandler
{
    public static async Task<Result<ReclassificacaoDto>> Handle(
        ObterReclassificacaoQuery query,
        IVerificadorDeAcesso verificador,
        IReclassificacaoRepository reclassificacoes,
        AutorizacaoDaReclassificacao autorizacao,
        DetalheDaReclassificacao detalhe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        ArgumentNullException.ThrowIfNull(autorizacao);
        ArgumentNullException.ThrowIfNull(detalhe);

        Result<Identidade?> autorizada = await autorizacao.AutorizarLeituraAsync(query.Id, verificador, cancellationToken).ConfigureAwait(false);

        return autorizada.IsFailure
            ? Result<ReclassificacaoDto>.Failure(autorizada.Error!)
            : await DetalheDaExistenteAsync(query.Id, autorizada.Value, reclassificacoes, detalhe, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>O detalhe para o leitor autorizado; a reclassificação inexistente é recusada antes de se montar o detalhe.</summary>
    private static async Task<Result<ReclassificacaoDto>> DetalheDaExistenteAsync(
        Guid id,
        Identidade? leitor,
        IReclassificacaoRepository reclassificacoes,
        DetalheDaReclassificacao detalhe,
        CancellationToken cancellationToken)
    {
        if (await reclassificacoes.ObterPerfilIdAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result<ReclassificacaoDto>.Failure(ReclassificacaoErros.NaoEncontrada());
        }

        return await detalhe.ObterAsync(id, leitor, cancellationToken).ConfigureAwait(false);
    }
}
