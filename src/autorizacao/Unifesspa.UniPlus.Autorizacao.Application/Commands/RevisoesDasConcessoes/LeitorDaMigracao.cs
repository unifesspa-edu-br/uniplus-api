namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// Lê as concessões alcançadas pela revisão, suas permissões atuais e a composição da versão de destino.
/// Quem chama já bloqueou o perfil e a revisão; as concessões são bloqueadas aqui em ordem de identificador.
/// O instante do contexto é lido após o último bloqueio.
/// </summary>
public sealed class LeitorDaMigracao
{
    private readonly IRevisaoDasConcessoesRepository _revisoes;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;
    private readonly TimeProvider _clock;

    public LeitorDaMigracao(IRevisaoDasConcessoesRepository revisoes, ICatalogoDePermissoesConsultavel catalogo, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(clock);

        _revisoes = revisoes;
        _catalogo = catalogo;
        _clock = clock;
    }

    public async Task<ContextoDaRevisao> LerAsync(Perfil perfil, RevisaoDasConcessoesDoPerfil revisao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(revisao);

        IReadOnlyList<Concessao> concessoes = await _revisoes
            .ObterConcessoesDaRevisaoAsync(perfil.Id, revisao.AbertaEm, cancellationToken)
            .ConfigureAwait(false);

        return await MontarAsync(perfil, revisao, _clock.GetUtcNow(), concessoes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>O mesmo contexto sobre concessões lidas sem rastreamento ou bloqueio, para a tela.</summary>
    public async Task<ContextoDaRevisao> LerParaLeituraAsync(
        Perfil perfil, RevisaoDasConcessoesDoPerfil revisao, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(revisao);

        IReadOnlyList<Concessao> concessoes = await _revisoes
            .ListarConcessoesDaRevisaoAsync(perfil.Id, revisao.AbertaEm, cancellationToken)
            .ConfigureAwait(false);

        return await MontarAsync(perfil, revisao, agora, concessoes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContextoDaRevisao> MontarAsync(
        Perfil perfil, RevisaoDasConcessoesDoPerfil revisao, DateTimeOffset agora, IReadOnlyList<Concessao> concessoes, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> composicoes = await _revisoes
            .ObterComposicoesAsync([.. concessoes.Select(static c => c.Id)], agora, cancellationToken)
            .ConfigureAwait(false);

        return new ContextoDaRevisao(agora, concessoes, composicoes, perfil.Versoes.First(v => v.Id == revisao.VersaoAlvoId).Codigos(), _catalogo, perfil.Versoes);
    }
}
