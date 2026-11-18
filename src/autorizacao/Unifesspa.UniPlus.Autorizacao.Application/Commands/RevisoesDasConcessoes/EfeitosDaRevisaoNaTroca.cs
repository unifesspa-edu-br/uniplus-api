namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que a entrada em vigor de uma versão faz à revisão das concessões do perfil, sob a trava e na transação de quem chama:
/// materializa a revisão que já não está efetivamente aberta, abre a revisão da versão que acrescenta permissão e leva o
/// destino das migrações para a versão reduzida quando uma remoção acontece dentro da revisão.
/// </summary>
public sealed class EfeitosDaRevisaoNaTroca
{
    private readonly IRevisaoDasConcessoesRepository _revisoes;
    private readonly LeitorDaMigracao _leitor;
    private readonly AvisosDaRevisao _avisos;
    private readonly AvisosDosEfeitosDaRevisao _efeitos;

    public EfeitosDaRevisaoNaTroca(
        IRevisaoDasConcessoesRepository revisoes, LeitorDaMigracao leitor, AvisosDaRevisao avisos, AvisosDosEfeitosDaRevisao efeitos)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(efeitos);

        _revisoes = revisoes;
        _leitor = leitor;
        _avisos = avisos;
        _efeitos = efeitos;
    }

    /// <summary>
    /// Materializa a revisão que já não está efetivamente aberta, para que nenhuma concessão vencida pelo prazo continue
    /// não revogada, na transação de quem chama. Devolve a revisão que segue aberta, se houver, e os avisos do fim.
    /// </summary>
    public async Task<RevisaoAposAMaterializacao> MaterializarSeVencidaAsync(Perfil perfil, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        RevisaoDasConcessoesDoPerfil? revisao = await _revisoes.ObterNaoEncerradaParaAlterarAsync(perfil.Id, cancellationToken).ConfigureAwait(false);

        return revisao is null
            ? new RevisaoAposAMaterializacao(null, [])
            : await MaterializarDaRevisaoAsync(perfil, revisao, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RevisaoAposAMaterializacao> MaterializarDaRevisaoAsync(
        Perfil perfil, RevisaoDasConcessoesDoPerfil revisao, CancellationToken cancellationToken)
    {
        ContextoDaRevisao contexto = await _leitor.LerAsync(perfil, revisao, cancellationToken).ConfigureAwait(false);

        if (revisao.EstaAberta(contexto.Agora, revisao.Pendencias(contexto.Concessoes, contexto.Agora)))
        {
            return new RevisaoAposAMaterializacao(revisao, []);
        }

        return new RevisaoAposAMaterializacao(null, await MaterializarAsync(new RevisaoDaOperacao(perfil, revisao, contexto), cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<EnviarEmail>> MaterializarAsync(RevisaoDaOperacao operacao, CancellationToken cancellationToken)
    {
        EstadoDaRevisaoAntesDaOperacao antes = EstadoDaRevisaoAntesDaOperacao.De(operacao.Revisao, operacao.Contexto);
        operacao.Revisao.ConferirEstado(operacao.Contexto);

        return await _efeitos
            .DoFimAsync(operacao, MudancasDaRevisaoDasConcessoes.Entre(antes, operacao.Revisao, operacao.Contexto), null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Abre a revisão da versão nova com as concessões elegíveis, que são as ativas do perfil fora dela. Sem nenhuma, a
    /// versão simplesmente vigora. Devolve se a revisão abriu e os avisos da abertura.
    /// </summary>
    public async Task<AberturaDaRevisaoDasConcessoes> AbrirAsync(
        Perfil perfil, TrocaDeVigencia troca, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);

        IReadOnlyList<Concessao> elegiveis = await ElegiveisParaAAberturaAsync(perfil.Id, troca.Nova, agora, cancellationToken).ConfigureAwait(false);
        if (elegiveis.Count == 0)
        {
            return new AberturaDaRevisaoDasConcessoes(false, []);
        }

        RevisaoDasConcessoesDoPerfil revisao = await AbrirRevisaoAsync(perfil, troca, agora, cancellationToken).ConfigureAwait(false);

        return new AberturaDaRevisaoDasConcessoes(
            true,
            await _avisos.DaAberturaAsync(perfil, revisao, elegiveis, troca.Nova.AcrescimosSobre(troca.Anterior!), cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<Concessao>> ElegiveisParaAAberturaAsync(
        Guid perfilId, VersaoDoPerfil nova, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        IReadOnlyList<Concessao> concessoes = await _revisoes.ObterConcessoesDaRevisaoAsync(perfilId, agora, cancellationToken).ConfigureAwait(false);

        return [.. concessoes.Where(concessao => concessao.PendenteDeMigrarPara(nova.Id, agora))];
    }

    private async Task<RevisaoDasConcessoesDoPerfil> AbrirRevisaoAsync(
        Perfil perfil, TrocaDeVigencia troca, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        RevisaoDasConcessoesDoPerfil revisao = RevisaoDasConcessoesDoPerfil.Abrir(perfil.Id, troca.Nova, agora);
        await _revisoes.AdicionarAsync(revisao, cancellationToken).ConfigureAwait(false);

        return revisao;
    }
}

/// <summary>A revisão que seguiu aberta depois da materialização, se houver, e os avisos do fim da que se encerrou.</summary>
public sealed record RevisaoAposAMaterializacao(RevisaoDasConcessoesDoPerfil? Aberta, IReadOnlyList<EnviarEmail> Avisos);

/// <summary>Se a troca abriu revisão das concessões, e os avisos da abertura.</summary>
public sealed record AberturaDaRevisaoDasConcessoes(bool Abriu, IReadOnlyList<EnviarEmail> Avisos);
