namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que a entrada em vigor de uma versão faz à volta do perfil: leva as concessões da versão que saiu para a nova,
/// quando ela não acrescenta permissão, e reavalia os pedidos ativos (<see cref="ReavaliacaoDosPedidosNaTroca"/>). Roda na
/// transação e com o perfil exclusivo de quem a chama, de modo que não há instante em que um pedido possa ser aprovado
/// contra uma versão já encerrada.
/// </summary>
/// <remarks>
/// A versão que acrescenta permissão deixa as concessões onde estão e abre a revisão das concessões: quem já tem o perfil
/// não recebe o acréscimo sem decisão, e a regra de decisão de acesso já impede que ele chegue por outro caminho. Só a
/// versão que sai migra; a concessão presa a uma versão ainda mais antiga continua nela. A remoção dentro da revisão
/// leva o destino das migrações para a versão reduzida.
/// </remarks>
public sealed class EfeitosDaTrocaDeVigencia
{
    private readonly IConcessaoRepository _concessoes;
    private readonly EfeitosDaRevisaoNaTroca _revisao;
    private readonly AvisosDosEfeitosDaRevisao _avisosDaVersao;
    private readonly ReavaliacaoDosPedidosNaTroca _reavaliacao;
    private readonly TimeProvider _clock;

    public EfeitosDaTrocaDeVigencia(
        IConcessaoRepository concessoes,
        EfeitosDaRevisaoNaTroca revisao,
        AvisosDosEfeitosDaRevisao avisosDaVersao,
        ReavaliacaoDosPedidosNaTroca reavaliacao,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(revisao);
        ArgumentNullException.ThrowIfNull(avisosDaVersao);
        ArgumentNullException.ThrowIfNull(reavaliacao);
        ArgumentNullException.ThrowIfNull(clock);

        _concessoes = concessoes;
        _revisao = revisao;
        _avisosDaVersao = avisosDaVersao;
        _reavaliacao = reavaliacao;
        _clock = clock;
    }

    /// <summary>Aplica os efeitos da troca e devolve os avisos, que entram no outbox junto da gravação.</summary>
    /// <param name="perfil">O perfil já trocado e travado pelo chamador.</param>
    /// <param name="troca">A troca de vigência; a primeira vigência do perfil não tem efeitos e não deve chegar aqui.</param>
    /// <param name="autor">Quem praticou o ato; não recebe o aviso da versão anterior encerrada.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task<IReadOnlyList<EnviarEmail>> AplicarAsync(
        Perfil perfil, TrocaDeVigencia troca, Identidade autor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);
        ArgumentNullException.ThrowIfNull(autor);

        VersaoDoPerfil anterior = troca.Anterior
            ?? throw new ArgumentException("A primeira vigência do perfil não tem efeitos a aplicar.", nameof(troca));

        RevisaoAposAMaterializacao materializada = await _revisao.MaterializarSeVencidaAsync(perfil, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> daTroca = await ConcessoesDaTrocaAsync(perfil, troca, materializada.Aberta, autor, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dosPedidos = await _reavaliacao.ReavaliarAsync(perfil, troca, cancellationToken).ConfigureAwait(false);

        return [.. materializada.Avisos, .. daTroca, .. dosPedidos];
    }

    /// <summary>
    /// Escolhe o efeito pela regra da troca: a versão com acréscimo abre a revisão, e a que não acrescenta leva as
    /// concessões. A versão anterior encerra com a troca quando não há revisão dela.
    /// </summary>
    private async Task<IReadOnlyList<EnviarEmail>> ConcessoesDaTrocaAsync(
        Perfil perfil, TrocaDeVigencia troca, RevisaoDasConcessoesDoPerfil? aberta, Identidade autor, CancellationToken cancellationToken) =>
        troca.TemAcrescimo
            ? await ComAcrescimoAsync(perfil, troca, autor, cancellationToken).ConfigureAwait(false)
            : await SemAcrescimoAsync(perfil, troca, aberta, autor, cancellationToken).ConfigureAwait(false);

    /// <summary>A versão com acréscimo abre a revisao; sem concessão a migrar, a versão anterior encerra com a troca.</summary>
    private async Task<IReadOnlyList<EnviarEmail>> ComAcrescimoAsync(
        Perfil perfil, TrocaDeVigencia troca, Identidade autor, CancellationToken cancellationToken)
    {
        AberturaDaRevisaoDasConcessoes abertura = await _revisao.AbrirAsync(perfil, troca, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return abertura.Abriu
            ? abertura.Avisos
            : await _avisosDaVersao.DaVersaoSubstituidaAsync(perfil, troca.Anterior!, FormaDoAviso.SemConcessaoAMigrar, autor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A versão sem acréscimo leva as concessões da que saiu para a nova e, dentro de uma revisao, faz da nova o destino das
    /// migrações; sem revisao, a versão anterior encerra com a troca.
    /// </summary>
    private async Task<IReadOnlyList<EnviarEmail>> SemAcrescimoAsync(
        Perfil perfil, TrocaDeVigencia troca, RevisaoDasConcessoesDoPerfil? aberta, Identidade autor, CancellationToken cancellationToken)
    {
        await MigrarConcessoesAsync(perfil.Id, troca.Anterior!, troca.Nova, cancellationToken).ConfigureAwait(false);
        if (aberta is not null)
        {
            aberta.RedirecionarAlvo(troca.Nova.Id);
            return [];
        }

        return await _avisosDaVersao.DaVersaoSubstituidaAsync(perfil, troca.Anterior!, FormaDoAviso.VersaoSubstituida, autor, cancellationToken).ConfigureAwait(false);
    }

    private async Task MigrarConcessoesAsync(
        Guid perfilId, VersaoDoPerfil anterior, VersaoDoPerfil nova, CancellationToken cancellationToken)
    {
        IReadOnlyList<Concessao> concessoes = await _concessoes
            .ObterParaMigrarAsync(perfilId, anterior.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (Concessao concessao in concessoes)
        {
            concessao.MigrarParaVersao(anterior, nova);
        }
    }
}
