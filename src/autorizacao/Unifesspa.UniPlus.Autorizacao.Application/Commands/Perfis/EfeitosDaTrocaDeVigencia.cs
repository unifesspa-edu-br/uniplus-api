namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que a entrada em vigor de uma versão faz à volta do perfil: leva as concessões da versão que saiu para a nova,
/// quando ela não acrescenta permissão, e reavalia os pedidos ativos. Roda na transação e sob a trava do perfil de quem a
/// chama, de modo que não há instante em que um pedido possa ser aprovado contra uma versão já encerrada.
/// </summary>
/// <remarks>
/// A versão que acrescenta permissão deixa as concessões onde estão e abre a janela de migração: quem já tem o perfil
/// não recebe o acréscimo sem decisão, e a regra de decisão de acesso já impede que ele chegue por outro caminho. Só a
/// versão que sai migra; a concessão presa a uma versão ainda mais antiga continua nela. A remoção dentro da janela
/// leva o destino das migrações para a versão reduzida.
/// </remarks>
public sealed class EfeitosDaTrocaDeVigencia
{
    private readonly IConcessaoRepository _concessoes;
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly AvisosDoPedido _avisos;
    private readonly AvisoDePedidoDevolvido _avisoDeDevolucao;
    private readonly EfeitosDaJanelaNaTroca _janela;
    private readonly AvisosDosEfeitosDaJanela _avisosDaVersao;
    private readonly TimeProvider _clock;

    public EfeitosDaTrocaDeVigencia(
        IConcessaoRepository concessoes,
        IPedidoDeConcessaoRepository pedidos,
        AvisosDoPedido avisos,
        AvisoDePedidoDevolvido avisoDeDevolucao,
        EfeitosDaJanelaNaTroca janela,
        AvisosDosEfeitosDaJanela avisosDaVersao,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(avisoDeDevolucao);
        ArgumentNullException.ThrowIfNull(janela);
        ArgumentNullException.ThrowIfNull(avisosDaVersao);
        ArgumentNullException.ThrowIfNull(clock);

        _concessoes = concessoes;
        _pedidos = pedidos;
        _avisos = avisos;
        _avisoDeDevolucao = avisoDeDevolucao;
        _janela = janela;
        _avisosDaVersao = avisosDaVersao;
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

        JanelaAposAMaterializacao materializada = await _janela.MaterializarSeVencidaAsync(perfil, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> daTroca = await ConcessoesDaTrocaAsync(perfil, troca, materializada.Aberta, autor, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> dosPedidos = await ReavaliarPedidosAsync(perfil, troca, cancellationToken).ConfigureAwait(false);

        return [.. materializada.Avisos, .. daTroca, .. dosPedidos];
    }

    // A versão com acréscimo abre a janela; a que não acrescenta leva as concessões da que saiu para a nova e, dentro de
    // uma janela, faz da nova o destino das migrações. A versão anterior encerra com a troca quando não há janela dela.
    private async Task<IReadOnlyList<EnviarEmail>> ConcessoesDaTrocaAsync(
        Perfil perfil, TrocaDeVigencia troca, JanelaDeMigracao? aberta, Identidade autor, CancellationToken cancellationToken)
    {
        if (troca.TemAcrescimo)
        {
            AberturaDaJanela abertura = await _janela.AbrirAsync(perfil, troca, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

            return abertura.Abriu
                ? abertura.Avisos
                : await EncerrarAVersaoAsync(perfil, troca, FormaDoAviso.SemConcessaoAMigrar, autor, cancellationToken).ConfigureAwait(false);
        }

        await MigrarConcessoesAsync(perfil.Id, troca.Anterior!, troca.Nova, cancellationToken).ConfigureAwait(false);
        if (aberta is not null)
        {
            aberta.RedirecionarAlvo(troca.Nova.Id);

            return [];
        }

        return await EncerrarAVersaoAsync(perfil, troca, FormaDoAviso.VersaoSubstituida, autor, cancellationToken).ConfigureAwait(false);
    }

    private Task<IReadOnlyList<EnviarEmail>> EncerrarAVersaoAsync(
        Perfil perfil, TrocaDeVigencia troca, FormaDoAviso forma, Identidade autor, CancellationToken cancellationToken) =>
        _avisosDaVersao.DaVersaoSubstituidaAsync(perfil, troca.Anterior!, forma, autor, cancellationToken);

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

    private async Task<IReadOnlyList<EnviarEmail>> ReavaliarPedidosAsync(
        Perfil perfil, TrocaDeVigencia troca, CancellationToken cancellationToken)
    {
        IReadOnlyList<PedidoDeConcessao> ativos = await _pedidos
            .ListarAtivosDoPerfilParaAlterarAsync(perfil.Id, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset instante = _clock.GetUtcNow();
        List<EnviarEmail> mensagens = [];

        foreach (PedidoDeConcessao pedido in ativos)
        {
            mensagens.AddRange(await TransitarAsync(pedido, perfil.Nome, troca, instante, cancellationToken).ConfigureAwait(false));
        }

        return mensagens;
    }

    private async Task<IReadOnlyList<EnviarEmail>> TransitarAsync(
        PedidoDeConcessao pedido,
        string perfilNome,
        TrocaDeVigencia troca,
        DateTimeOffset instante,
        CancellationToken cancellationToken)
    {
        SituacaoDoPedido situacaoAnterior = pedido.Situacao;
        DesfechoDaTrocaDeVersao desfecho = pedido.AoEntrarEmVigor(troca.Nova, troca.Anterior!.Numero, troca.TemAcrescimo, instante);

        return await AvisarAsync(pedido, perfilNome, situacaoAnterior, troca, desfecho, cancellationToken).ConfigureAwait(false);
    }

    // Só o pedido devolvido e o que volta ao gestor mudam de mãos; o que apenas troca de versão segue sem aviso.
    private async Task<IReadOnlyList<EnviarEmail>> AvisarAsync(
        PedidoDeConcessao pedido,
        string perfilNome,
        SituacaoDoPedido situacaoAnterior,
        TrocaDeVigencia troca,
        DesfechoDaTrocaDeVersao desfecho,
        CancellationToken cancellationToken) =>
        desfecho switch
        {
            DesfechoDaTrocaDeVersao.Devolvido => await _avisoDeDevolucao.AvisarAsync(pedido, perfilNome, troca.Nova, cancellationToken).ConfigureAwait(false),
            DesfechoDaTrocaDeVersao.VoltouAoGestor => await _avisos.AvisarAsync(pedido, perfilNome, situacaoAnterior, cancellationToken).ConfigureAwait(false),
            _ => [],
        };
}
