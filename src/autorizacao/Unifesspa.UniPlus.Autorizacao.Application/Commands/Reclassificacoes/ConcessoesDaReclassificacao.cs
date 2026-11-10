namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Reclassificacoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Uma concessão suspensa pela reclassificação e o que as etapas já decidiram sobre ela.</summary>
public sealed record ConcessaoSuspensa(ConcessaoComAPermissao Item, IReadOnlyList<DecisaoNaConcessao> Decisoes)
{
    /// <summary>A concessão aguarda a etapa: a base dela a exige da reclassificação e ela ainda não foi decidida.</summary>
    public bool Aguarda(ReclassificacaoDaPermissao reclassificacao, EtapaDaVersao etapa)
    {
        ArgumentNullException.ThrowIfNull(reclassificacao);

        return reclassificacao.EtapasExigidas(Item.Base).Contains(etapa) && !Decisoes.Any(d => d.Etapa == etapa);
    }
}

/// <summary>
/// As concessões do perfil que a reclassificação suspende, lidas do banco: as que reúnem a permissão pela composição e cuja
/// base a reclassificação ultrapassa, cada uma com as etapas que já decidiram sobre ela.
/// </summary>
public sealed class ConcessoesDaReclassificacao
{
    private readonly IReclassificacaoRepository _reclassificacoes;

    public ConcessoesDaReclassificacao(IReclassificacaoRepository reclassificacoes)
    {
        ArgumentNullException.ThrowIfNull(reclassificacoes);
        _reclassificacoes = reclassificacoes;
    }

    /// <summary>Todas as concessões com a permissão, suspensas ou não, com as decisões da reclassificação sobre cada uma.</summary>
    public async Task<IReadOnlyList<ConcessaoSuspensa>> LerComAPermissaoAsync(
        ReclassificacaoDaPermissao reclassificacao, DateTimeOffset agora, bool travar, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reclassificacao);

        IReadOnlyList<ConcessaoComAPermissao> concessoes = await _reclassificacoes
            .ListarConcessoesComAPermissaoAsync(reclassificacao.PerfilId, reclassificacao.Codigo, agora, travar, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<LiberacaoDaReclassificacao> liberacoes = await _reclassificacoes
            .ListarLiberacoesAsync(reclassificacao.Id, cancellationToken).ConfigureAwait(false);

        return
        [.. concessoes.Select(c => new ConcessaoSuspensa(
            c, [.. liberacoes.Where(l => l.ConcessaoId == c.Concessao.Id).Select(static l => new DecisaoNaConcessao(l.Etapa, l.Decisor, l.Resultado))]))];
    }

    /// <summary>Só as suspensas pela reclassificação.</summary>
    public async Task<IReadOnlyList<ConcessaoSuspensa>> LerSuspensasAsync(
        ReclassificacaoDaPermissao reclassificacao, DateTimeOffset agora, CancellationToken cancellationToken) =>
        [.. (await LerComAPermissaoAsync(reclassificacao, agora, false, cancellationToken).ConfigureAwait(false))
            .Where(c => reclassificacao.SuspendeA(c.Item.Base))];
}
