namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que a entrada em vigor de uma versão faz à janela de migração do perfil, sob a trava e na transação de quem chama:
/// materializa a janela que já não está efetivamente aberta, abre a janela da versão que acrescenta permissão e leva o
/// destino das migrações para a versão reduzida quando uma remoção acontece dentro da janela.
/// </summary>
public sealed class EfeitosDaJanelaNaTroca
{
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly LeitorDaMigracao _leitor;
    private readonly AvisosDaJanela _avisos;
    private readonly AvisosDosEfeitosDaJanela _efeitos;

    public EfeitosDaJanelaNaTroca(
        IJanelaDeMigracaoRepository janelas, LeitorDaMigracao leitor, AvisosDaJanela avisos, AvisosDosEfeitosDaJanela efeitos)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(efeitos);

        _janelas = janelas;
        _leitor = leitor;
        _avisos = avisos;
        _efeitos = efeitos;
    }

    /// <summary>
    /// Materializa a janela que já não está efetivamente aberta, para que nenhuma concessão vencida pelo prazo continue
    /// não revogada, na transação de quem chama. Devolve a janela que segue aberta, se houver, e os avisos do fim.
    /// </summary>
    public async Task<JanelaAposAMaterializacao> MaterializarSeVencidaAsync(Perfil perfil, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        JanelaDeMigracao? janela = await _janelas.ObterNaoEncerradaParaAlterarAsync(perfil.Id, cancellationToken).ConfigureAwait(false);
        if (janela is null)
        {
            return new JanelaAposAMaterializacao(null, []);
        }

        ContextoDaMigracao contexto = await _leitor.LerAsync(perfil, janela, cancellationToken).ConfigureAwait(false);
        if (janela.EstaAberta(contexto.Agora, janela.Pendencias(contexto.Concessoes, contexto.Agora)))
        {
            return new JanelaAposAMaterializacao(janela, []);
        }

        return new JanelaAposAMaterializacao(null, await MaterializarAsync(new JanelaDaOperacao(perfil, janela, contexto), cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<EnviarEmail>> MaterializarAsync(JanelaDaOperacao operacao, CancellationToken cancellationToken)
    {
        EstadoDaJanelaAntesDaOperacao antes = EstadoDaJanelaAntesDaOperacao.De(operacao.Janela, operacao.Contexto);
        operacao.Janela.ConferirEstado(operacao.Contexto);

        return await _efeitos
            .DoFimAsync(operacao, MudancasDaJanela.Entre(antes, operacao.Janela, operacao.Contexto), null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Abre a janela da versão nova com as concessões elegíveis, que são as ativas do perfil fora dela. Sem nenhuma, a
    /// versão simplesmente vigora. Devolve se a janela abriu e os avisos da abertura.
    /// </summary>
    public async Task<AberturaDaJanela> AbrirAsync(
        Perfil perfil, TrocaDeVigencia troca, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);

        IReadOnlyList<Concessao> concessoes = await _janelas.ObterConcessoesDaJanelaAsync(perfil.Id, agora, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Concessao> elegiveis = [.. concessoes.Where(c => c.EstaAtiva(agora) && c.VersaoId != troca.Nova.Id)];
        if (elegiveis.Count == 0)
        {
            return new AberturaDaJanela(false, []);
        }

        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(perfil.Id, troca.Nova, agora);
        await _janelas.AdicionarAsync(janela, cancellationToken).ConfigureAwait(false);

        return new AberturaDaJanela(
            true,
            await _avisos.DaAberturaAsync(perfil, janela, elegiveis, troca.Nova.AcrescimosSobre(troca.Anterior!), cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>A janela que seguiu aberta depois da materialização, se houver, e os avisos do fim da que se encerrou.</summary>
public sealed record JanelaAposAMaterializacao(JanelaDeMigracao? Aberta, IReadOnlyList<EnviarEmail> Avisos);

/// <summary>Se a troca abriu janela de migração, e os avisos da abertura.</summary>
public sealed record AberturaDaJanela(bool Abriu, IReadOnlyList<EnviarEmail> Avisos);
