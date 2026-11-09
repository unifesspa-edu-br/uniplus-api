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

    public EfeitosDaJanelaNaTroca(IJanelaDeMigracaoRepository janelas, LeitorDaMigracao leitor, AvisosDaJanela avisos)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(avisos);

        _janelas = janelas;
        _leitor = leitor;
        _avisos = avisos;
    }

    /// <summary>
    /// Materializa a janela que já não está efetivamente aberta, para que nenhuma concessão vencida pelo prazo continue
    /// não revogada, e devolve a janela que segue aberta, se houver.
    /// </summary>
    public async Task<JanelaDeMigracao?> MaterializarSeVencidaAsync(Perfil perfil, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        JanelaDeMigracao? janela = await _janelas.ObterNaoEncerradaParaAlterarAsync(perfil.Id, cancellationToken).ConfigureAwait(false);
        if (janela is null)
        {
            return null;
        }

        ContextoDaMigracao contexto = await _leitor.LerAsync(perfil, janela, agora, cancellationToken).ConfigureAwait(false);
        if (janela.EstaAberta(agora, janela.Pendencias(contexto.Concessoes, agora)))
        {
            return janela;
        }

        janela.ConferirEstado(contexto);

        return null;
    }

    /// <summary>
    /// Abre a janela da versão nova com as concessões elegíveis, que são as ativas do perfil fora dela. Sem nenhuma, a
    /// versão simplesmente vigora. Devolve os avisos da abertura.
    /// </summary>
    public async Task<IReadOnlyList<EnviarEmail>> AbrirAsync(
        Perfil perfil, TrocaDeVigencia troca, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);

        IReadOnlyList<Concessao> concessoes = await _janelas.ObterConcessoesDaJanelaAsync(perfil.Id, agora, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Concessao> elegiveis = [.. concessoes.Where(c => c.EstaAtiva(agora) && c.VersaoId != troca.Nova.Id)];
        if (elegiveis.Count == 0)
        {
            return [];
        }

        JanelaDeMigracao janela = JanelaDeMigracao.Abrir(perfil.Id, troca.Nova, agora);
        await _janelas.AdicionarAsync(janela, cancellationToken).ConfigureAwait(false);

        return await _avisos
            .DaAberturaAsync(perfil, janela, elegiveis, troca.Nova.AcrescimosSobre(troca.Anterior!), cancellationToken)
            .ConfigureAwait(false);
    }
}
