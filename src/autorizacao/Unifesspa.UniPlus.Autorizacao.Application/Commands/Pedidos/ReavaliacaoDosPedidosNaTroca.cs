namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A reavaliação dos pedidos ativos do perfil quando uma versão nova entra em vigor: cada pedido segue para a versão nova,
/// é devolvido ou volta ao gestor, conforme a regra do pedido. Roda na transação e com o perfil exclusivo de quem a chama,
/// de modo que não há instante em que um pedido possa ser aprovado contra uma versão já encerrada. Nada se grava aqui.
/// </summary>
public sealed class ReavaliacaoDosPedidosNaTroca
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly AvisosDoPedido _avisos;
    private readonly AvisoDePedidoDevolvido _avisoDeDevolucao;
    private readonly TimeProvider _clock;

    public ReavaliacaoDosPedidosNaTroca(
        IPedidoDeConcessaoRepository pedidos, AvisosDoPedido avisos, AvisoDePedidoDevolvido avisoDeDevolucao, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(avisoDeDevolucao);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _avisos = avisos;
        _avisoDeDevolucao = avisoDeDevolucao;
        _clock = clock;
    }

    /// <summary>Reavalia os pedidos ativos do perfil contra a troca e devolve os avisos, que entram no outbox junto da gravação.</summary>
    public async Task<IReadOnlyList<EnviarEmail>> ReavaliarAsync(Perfil perfil, TrocaDeVigencia troca, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(troca);

        IReadOnlyList<PedidoDeConcessao> ativos = await _pedidos
            .ListarAtivosDoPerfilParaAlterarAsync(perfil.Id, cancellationToken)
            .ConfigureAwait(false);

        return await TransitarAsync(ativos, perfil.Nome, troca, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<EnviarEmail>> TransitarAsync(
        IReadOnlyList<PedidoDeConcessao> ativos, string perfilNome, TrocaDeVigencia troca, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        List<EnviarEmail> mensagens = [];

        foreach (PedidoDeConcessao pedido in ativos)
        {
            SituacaoDoPedido situacaoAnterior = pedido.Andamento.Situacao;
            DesfechoDaTrocaDeVersao desfecho = pedido.AoEntrarEmVigor(troca, instante);
            mensagens.AddRange(await AvisarAsync(pedido, perfilNome, situacaoAnterior, troca, desfecho, cancellationToken).ConfigureAwait(false));
        }

        return mensagens;
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
