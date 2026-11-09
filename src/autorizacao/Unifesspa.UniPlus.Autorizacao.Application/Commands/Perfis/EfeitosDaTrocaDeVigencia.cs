namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Application.Abstractions.Email;
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
/// A versão que acrescenta permissão deixa as concessões onde estão: quem já tem o perfil não recebe o acréscimo sem
/// aprovação, e a regra de decisão de acesso já impede que ele chegue por outro caminho. Só a versão que sai migra; a
/// concessão presa a uma versão ainda mais antiga continua nela.
/// </remarks>
public sealed class EfeitosDaTrocaDeVigencia
{
    private readonly IConcessaoRepository _concessoes;
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly AvisosDoPedido _avisos;
    private readonly TimeProvider _clock;

    public EfeitosDaTrocaDeVigencia(
        IConcessaoRepository concessoes, IPedidoDeConcessaoRepository pedidos, AvisosDoPedido avisos, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(clock);

        _concessoes = concessoes;
        _pedidos = pedidos;
        _avisos = avisos;
        _clock = clock;
    }

    /// <summary>Aplica os efeitos da troca e devolve os avisos, que entram no outbox junto da gravação.</summary>
    /// <param name="perfil">O perfil já trocado e travado pelo chamador.</param>
    /// <param name="troca">A troca de vigência; a primeira vigência do perfil não tem efeitos e não deve chegar aqui.</param>
    public async Task<IReadOnlyList<EnviarEmail>> AplicarAsync(Perfil perfil, TrocaDeVigencia troca, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);

        VersaoDoPerfil anterior = troca.Anterior
            ?? throw new ArgumentException("A primeira vigência do perfil não tem efeitos a aplicar.", nameof(troca));

        if (!troca.TemAcrescimo)
        {
            await MigrarConcessoesAsync(perfil.Id, anterior, troca.Nova, cancellationToken).ConfigureAwait(false);
        }

        return await ReavaliarPedidosAsync(perfil, troca, anterior, cancellationToken).ConfigureAwait(false);
    }

    private async Task MigrarConcessoesAsync(
        Guid perfilId, VersaoDoPerfil anterior, VersaoDoPerfil nova, CancellationToken cancellationToken)
    {
        IReadOnlyList<Concessao> concessoes = await _concessoes
            .ObterParaMigrarAsync(perfilId, anterior.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (Concessao concessao in concessoes)
        {
            concessao.MigrarParaVersao(nova.Id, anterior.Id);
        }
    }

    private async Task<IReadOnlyList<EnviarEmail>> ReavaliarPedidosAsync(
        Perfil perfil, TrocaDeVigencia troca, VersaoDoPerfil anterior, CancellationToken cancellationToken)
    {
        IReadOnlyList<PedidoDeConcessao> ativos = await _pedidos
            .ListarAtivosDoPerfilParaAlterarAsync(perfil.Id, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset instante = _clock.GetUtcNow();
        List<EnviarEmail> mensagens = [];

        foreach (PedidoDeConcessao pedido in ativos)
        {
            SituacaoDoPedido situacaoAnterior = pedido.Situacao;
            DesfechoDaTrocaDeVersao desfecho = pedido.AoEntrarEmVigor(troca.Nova, anterior.Numero, troca.TemAcrescimo, instante);

            mensagens.AddRange(await AvisarAsync(pedido, perfil.Nome, situacaoAnterior, desfecho, cancellationToken).ConfigureAwait(false));
        }

        return mensagens;
    }

    // Só o pedido devolvido e o que volta ao gestor mudam de mãos; o que apenas troca de versão segue sem aviso.
    private async Task<IReadOnlyList<EnviarEmail>> AvisarAsync(
        PedidoDeConcessao pedido,
        string perfilNome,
        SituacaoDoPedido situacaoAnterior,
        DesfechoDaTrocaDeVersao desfecho,
        CancellationToken cancellationToken) =>
        desfecho is DesfechoDaTrocaDeVersao.Devolvido or DesfechoDaTrocaDeVersao.VoltouAoGestor
            ? await _avisos.AvisarAsync(pedido, perfilNome, situacaoAnterior, cancellationToken).ConfigureAwait(false)
            : [];
}
