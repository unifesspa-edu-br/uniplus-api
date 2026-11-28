namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Prepara o item do edital sem escrever e junta todas as recusas que não dependem umas das outras: a forma do pedido,
/// o item, os documentos, a política de divulgação e o ato corrigido. Nenhuma consulta lê um valor já recusado, e nenhuma
/// corre quando o processo ou o instante foi recusado.
/// </summary>
public sealed class PreparacaoDaPublicacaoDoProdutoDoEdital(
    ConferenciaDoPedidoDoAto pedidos,
    ConferenciaDoProdutoDoEdital produtos)
{
    internal async Task<Result<AtoAPublicar>> PrepararAsync(PublicarProdutoDoEditalCommand command, CancellationToken cancellationToken)
    {
        PedidoDoAtoConferido pedido = await pedidos.ConferirAsync(command, [], cancellationToken).ConfigureAwait(false);
        ProdutoDoEditalConferido produto = pedido.Estado is { } estado
            ? await produtos.ConferirAsync(command, pedido.Pedido.Correcao, estado.Instante, cancellationToken).ConfigureAwait(false)
            : ProdutoDoEditalConferido.NaoConsultado;

        return pedido.Reunir(
            produto.DoItem,
            [.. produto.DaPolitica, .. produto.DaCorrecao],
            () => pedido.Ato(produto.Tipo!, produto.Item, produto.ContaId));
    }
}
