namespace Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Comprovantes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Wolverine.Attributes;

/// <summary>
/// Emite o comprovante de uma inscrição do processo pela unidade que o opera, inclusive o da inscrição cancelada, que o
/// titular não recebe. A ordem das recusas é fixa: o vínculo com o processo e consultar as inscrições respondem o mesmo 404
/// do que não existe; só depois, já sabendo que a pessoa alcança a inscrição, a falta da permissão de emitir responde 403.
/// O comprovante é lido e decifrado depois das duas decisões, e só o pedido.
/// </summary>
public static class EmitirComprovanteDaInscricaoQueryHandler
{
    /// <summary>
    /// <c>[NonTransactional]</c>: o verificador de acesso lê as concessões do <c>AutorizacaoDbContext</c> e os repositórios
    /// leem o <c>SelecaoDbContext</c>; o detector de transação do Wolverine recusa dois contextos. A consulta só lê.
    /// </summary>
    [NonTransactional]
    public static async Task<Result<ComprovanteDaInscricaoDto>> Handle(
        EmitirComprovanteDaInscricaoQuery query,
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processoRepository,
        IInscricaoRepository inscricaoRepository,
        MontadorDoComprovante montador,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(processoRepository);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(montador);

        ConferenciaDoVinculo conferencia = await AcessoAInscricao
            .ConferirVinculoAsync(processoRepository, inscricaoRepository, query.ProcessoSeletivoId, query.InscricaoId, cancellationToken)
            .ConfigureAwait(false);

        DomainError? recusa = conferencia is Vinculo vinculo
            ? await RecusaDoAcessoAsync(verificador, vinculo, query, cancellationToken).ConfigureAwait(false)
            : await RecusaDoInexistenteAsync(verificador, conferencia, query, cancellationToken).ConfigureAwait(false);

        return recusa is not null
            ? Result<ComprovanteDaInscricaoDto>.Failure(recusa)
            : await EmitirAsync(query, inscricaoRepository, montador, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainError?> RecusaDoInexistenteAsync(
        IVerificadorDeAcesso verificador, ConferenciaDoVinculo conferencia, EmitirComprovanteDaInscricaoQuery query, CancellationToken cancellationToken)
    {
        await AcessoAInscricao
            .RegistrarNegativaDoInexistenteAsync(
                verificador,
                conferencia,
                query.ProcessoSeletivoId,
                [PermissaoSelecao.InscricoesConsultar.Requisito, PermissaoSelecao.InscricoesEmitirComprovante.Requisito],
                cancellationToken)
            .ConfigureAwait(false);

        return AcessoAInscricao.NaoEncontradaNoProcesso;
    }

    private static async Task<DomainError?> RecusaDoAcessoAsync(
        IVerificadorDeAcesso verificador, Vinculo vinculo, EmitirComprovanteDaInscricaoQuery query, CancellationToken cancellationToken)
    {
        ResourceContext resource = AcessoAoProcesso.ResourceDaInscricao(vinculo.UnidadeId, query.ProcessoSeletivoId, query.InscricaoId);

        if (await AcessoAInscricao
            .DecidirAsync(verificador, vinculo, PermissaoSelecao.InscricoesConsultar.Requisito, resource, cancellationToken)
            .ConfigureAwait(false) is Recusada semConsultar)
        {
            return AcessoAInscricao.Erro(semConsultar, AcessoAInscricao.NaoEncontradaNoProcesso);
        }

        return await AcessoAInscricao
            .DecidirAsync(verificador, vinculo, PermissaoSelecao.InscricoesEmitirComprovante.Requisito, resource, cancellationToken)
            .ConfigureAwait(false) is Recusada semEmitir
            ? AcessoAoProcesso.Recusa(semEmitir.Resultado)
            : null;
    }

    private static async Task<Result<ComprovanteDaInscricaoDto>> EmitirAsync(
        EmitirComprovanteDaInscricaoQuery query, IInscricaoRepository inscricaoRepository, MontadorDoComprovante montador, CancellationToken cancellationToken)
    {
        Inscricao? inscricao = await inscricaoRepository.ObterPorIdAsync(query.InscricaoId, cancellationToken).ConfigureAwait(false);

        return inscricao is null
            ? Result<ComprovanteDaInscricaoDto>.Failure(AcessoAInscricao.NaoEncontradaNoProcesso)
            : await ComprovanteDoNumero.MontarAsync(inscricao, query.Numero, inscricaoRepository, montador, cancellationToken).ConfigureAwait(false);
    }
}
