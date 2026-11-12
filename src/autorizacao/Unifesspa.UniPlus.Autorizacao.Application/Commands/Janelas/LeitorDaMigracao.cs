namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Lê do banco o que a janela não vê sozinha e monta o contexto da migração: as concessões do perfil que ela alcança,
/// o que cada uma dá hoje e a composição da versão-alvo. Quem chama já tem o perfil e a janela travados, e as concessões são travadas aqui, em ordem de id. O instante do contexto é lido
/// depois da última trava, o único ponto em que a operação o lê.
/// </summary>
public sealed class LeitorDaMigracao
{
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly TimeProvider _clock;

    public LeitorDaMigracao(IJanelaDeMigracaoRepository janelas, ICatalogoDePermissoesConsultavel catalogo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _janelas = janelas;
        _catalogo = catalogo;
        _clock = clock;
    }

    public async Task<ContextoDaMigracao> LerAsync(Perfil perfil, JanelaDeMigracao janela, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);

        IReadOnlyList<Concessao> concessoes = await _janelas
            .ObterConcessoesDaJanelaAsync(perfil.Id, janela.AbertaEm, cancellationToken)
            .ConfigureAwait(false);

        return await MontarAsync(perfil, janela, _clock.GetUtcNow(), concessoes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>O mesmo contexto sobre concessões lidas sem rastrear nem travar, para a leitura da tela.</summary>
    public async Task<ContextoDaMigracao> LerParaLeituraAsync(
        Perfil perfil, JanelaDeMigracao janela, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(janela);

        IReadOnlyList<Concessao> concessoes = await _janelas
            .ListarConcessoesDaJanelaAsync(perfil.Id, janela.AbertaEm, cancellationToken)
            .ConfigureAwait(false);

        return await MontarAsync(perfil, janela, agora, concessoes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContextoDaMigracao> MontarAsync(
        Perfil perfil, JanelaDeMigracao janela, DateTimeOffset agora, IReadOnlyList<Concessao> concessoes, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> composicoes = await _janelas
            .ObterComposicoesAsync([.. concessoes.Select(static c => c.Id)], agora, cancellationToken)
            .ConfigureAwait(false);

        return new ContextoDaMigracao(agora, concessoes, composicoes, perfil.Versoes.First(v => v.Id == janela.VersaoAlvoId).Codigos(), _catalogo, perfil.Versoes);
    }
}
