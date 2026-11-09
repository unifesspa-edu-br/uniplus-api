namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A janela de migração aberta do perfil, lida sob as travas, com o contexto que a operação precisa.</summary>
public sealed record JanelaDaOperacao(Perfil Perfil, JanelaDeMigracao Janela, ContextoDaMigracao Contexto);

/// <summary>
/// Executa uma operação sobre a janela de migração na ordem de travas única: o perfil de governança da proteção de dados
/// e as concessões ativas dele, quando a dispensa pode valer, depois o perfil em migração, a recarga e as concessões
/// elegíveis. O instante é lido depois da última trava, na leitura das concessões. Antes de qualquer outra coisa roda a conferência do estado da janela:
/// o prazo vencido e a falta de pendência materializam o fim e a operação responde a recusa, que se grava mesmo assim.
/// </summary>
public sealed class MigracaoSobreOPerfil
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly LeitorDaMigracao _leitor;

    public MigracaoSobreOPerfil(OperacaoSobreOPerfil operacao, IJanelaDeMigracaoRepository janelas, LeitorDaMigracao leitor)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(leitor);

        _operacao = operacao;
        _janelas = janelas;
        _leitor = leitor;
    }

    /// <param name="perfilId">O perfil em migração.</param>
    /// <param name="travaProtecaoDeDados">A operação lê a dispensa e trava antes o perfil de governança da proteção de dados.</param>
    /// <param name="operacao">A transição, sobre a janela e o contexto já conferidos.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task<GravacaoDaOperacao<T>> ExecutarAsync<T>(
        Guid perfilId,
        bool travaProtecaoDeDados,
        Func<JanelaDaOperacao, Task<DesfechoDaOperacao<T>>> operacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        if (travaProtecaoDeDados)
        {
            await _janelas.TravarProtecaoDeDadosAsync(cancellationToken).ConfigureAwait(false);
        }

        return await _operacao
            .ExecutarComAvisosAsync(
                perfilId, (perfil, _) => NaTravaAsync(perfil, operacao, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
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

        return janela.ConferirEstado(contexto) is { } recusa
            ? new DesfechoDaOperacao<T>(Result<T>.Failure(recusa), GravarMesmoNaFalha: true)
            : await operacao(new JanelaDaOperacao(perfil, janela, contexto)).ConfigureAwait(false);
    }
}
