namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A janela de migração aberta do perfil, lida sob as travas, com o contexto que a operação precisa.</summary>
public sealed record JanelaDaOperacao(Perfil Perfil, JanelaDeMigracao Janela, ContextoDaMigracao Contexto);

/// <summary>
/// Executa uma operação sobre a janela de migração na ordem de travas única: o perfil em migração, a recarga e as
/// concessões elegíveis. A janela não toma a trava do papel de Representante do controlador, porque nunca concede nem
/// revoga o perfil dele (ADR-0162). O instante é lido depois da última trava, na leitura das concessões. Antes de qualquer outra coisa roda a conferência do estado da janela:
/// o prazo vencido e a falta de pendência materializam o fim e a operação responde a recusa, que se grava mesmo assim.
/// </summary>
public sealed class MigracaoSobreOPerfil
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly LeitorDaMigracao _leitor;
    private readonly AvisosDosEfeitosDaJanela _avisos;

    public MigracaoSobreOPerfil(
        OperacaoSobreOPerfil operacao, IJanelaDeMigracaoRepository janelas, LeitorDaMigracao leitor, AvisosDosEfeitosDaJanela avisos)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(avisos);

        _operacao = operacao;
        _janelas = janelas;
        _leitor = leitor;
        _avisos = avisos;
    }

    /// <summary>A operação de endpoint com chave de idempotência: o conflito de concorrência volta como recusa.</summary>
    /// <param name="perfilId">O perfil em migração.</param>
    /// <param name="operacao">A transição, sobre a janela e o contexto já conferidos.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public Task<GravacaoDaOperacao<T>> ExecutarAsync<T>(
        Guid perfilId,
        Func<JanelaDaOperacao, Task<DesfechoDaOperacao<T>>> operacao,
        CancellationToken cancellationToken) =>
        ExecutarAsync(perfilId, operacao, _operacao.ExecutarComAvisosAsync, cancellationToken);

    /// <summary>A operação sem chave de idempotência, como a rotina: o conflito de concorrência propaga.</summary>
    /// <param name="perfilId">O perfil em migração.</param>
    /// <param name="operacao">A transição, sobre a janela e o contexto já conferidos.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public Task<GravacaoDaOperacao<T>> ExecutarSemChaveAsync<T>(
        Guid perfilId, Func<JanelaDaOperacao, Task<DesfechoDaOperacao<T>>> operacao, CancellationToken cancellationToken) =>
        ExecutarAsync(perfilId, operacao, _operacao.ExecutarComAvisosSemChaveAsync, cancellationToken);

    private Task<GravacaoDaOperacao<T>> ExecutarAsync<T>(
        Guid perfilId,
        Func<JanelaDaOperacao, Task<DesfechoDaOperacao<T>>> operacao,
        Func<Guid, Func<Perfil, ContextoDaOperacao, Task<DesfechoDaOperacao<T>>>, CancellationToken, Task<GravacaoDaOperacao<T>>> executar,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        return executar(perfilId, (perfil, _) => NaTravaAsync(perfil, operacao, cancellationToken), cancellationToken);
    }

    private async Task<DesfechoDaOperacao<T>> NaTravaAsync<T>(
        Perfil perfil, Func<JanelaDaOperacao, Task<DesfechoDaOperacao<T>>> operacao, CancellationToken cancellationToken)
    {
        JanelaDeMigracao? janela = await _janelas.ObterNaoEncerradaParaAlterarAsync(perfil.Id, cancellationToken).ConfigureAwait(false);
        if (janela is null)
        {
            return new DesfechoDaOperacao<T>(Result<T>.Failure(MigracaoErros.JanelaNaoEncontrada()));
        }

        ContextoDaMigracao contexto = await _leitor.LerAsync(perfil, janela, cancellationToken).ConfigureAwait(false);
        JanelaDaOperacao daOperacao = new(perfil, janela, contexto);
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(janela, contexto);

        return janela.ConferirEstado(contexto) is { } recusa
            ? new DesfechoDaOperacao<T>(Result<T>.Failure(recusa), GravarMesmoNaFalha: true, Avisos: await AvisosDoFimAsync(daOperacao, antes, cancellationToken).ConfigureAwait(false))
            : await operacao(daOperacao).ConfigureAwait(false);
    }

    // O fim que a conferência materializou avisa a quem perdeu o acesso e aos grupos, sem exclusão: ninguém praticou o ato.
    private Task<IReadOnlyList<EnviarEmail>> AvisosDoFimAsync(JanelaDaOperacao janela, EstadoDaJanelaAntesDaOperacao antes, CancellationToken cancellationToken) =>
        _avisos.DoFimAsync(janela, MudancasDaJanela.Entre(antes, janela.Janela, janela.Contexto), null, cancellationToken);
}
