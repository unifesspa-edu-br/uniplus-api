namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A recusa da etapa que o pedido aguarda: encerra o pedido com o motivo, sem concessão. Quem decide e a permissão da
/// etapa se conferem como na aprovação. O aviso do desfecho sai como mensagem de saída, no outbox da mesma transação, e não
/// leva o motivo.
/// </summary>
public sealed class RecusaDoPedido
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IConcessaoRepository _concessoes;
    private readonly CatalogoDaAplicacao _catalogo;
    private readonly GravacaoDaDecisaoDoPedido _gravacao;
    private readonly TimeProvider _clock;

    public RecusaDoPedido(
        IPedidoDeConcessaoRepository pedidos,
        IConcessaoRepository concessoes,
        CatalogoDaAplicacao catalogo,
        GravacaoDaDecisaoDoPedido gravacao,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(concessoes);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _concessoes = concessoes;
        _catalogo = catalogo;
        _gravacao = gravacao;
        _clock = clock;
    }

    public async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> RecusarAsync(
        RecusarPedidoDeConcessaoCommand command, IVerificadorDeAcesso verificador, Identidade? decisor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<DecisaoDaEtapa> preparada = await DecisaoDaEtapa
            .PrepararAsync(command.Id, _pedidos, _concessoes, verificador, _catalogo, decisor, cancellationToken)
            .ConfigureAwait(false);

        return preparada.IsFailure
            ? GravacaoDaDecisaoDoPedido.Recusa(preparada.Error!)
            : await RecusarNaEtapaAsync(preparada.Value!, command.Motivo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> RecusarNaEtapaAsync(
        DecisaoDaEtapa decisao, string? motivo, CancellationToken cancellationToken)
    {
        SituacaoDoPedido situacaoAnterior = decisao.Pedido.Andamento.Situacao;
        Result recusa = decisao.Pedido.Recusar(decisao.Decisor, motivo, decisao.Avaliado, _clock.GetUtcNow());

        return await _gravacao.GravarRecusaAsync(decisao, recusa, situacaoAnterior, cancellationToken).ConfigureAwait(false);
    }
}
