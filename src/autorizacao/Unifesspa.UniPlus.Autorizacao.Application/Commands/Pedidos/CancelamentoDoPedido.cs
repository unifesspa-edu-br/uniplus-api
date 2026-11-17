namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O cancelamento do pedido pelo solicitante. O pedido de outra pessoa não se distingue do inexistente. O endpoint não leva
/// <c>Idempotency-Key</c>: o conflito de concorrência propaga e o middleware o responde como 409 (ADR-0119). O aviso ao
/// beneficiário sai como mensagem de saída, no outbox da mesma transação.
/// </summary>
public sealed class CancelamentoDoPedido
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IPerfilRepository _perfis;
    private readonly AvisosDoPedido _avisos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public CancelamentoDoPedido(
        IPedidoDeConcessaoRepository pedidos,
        IPerfilRepository perfis,
        AvisosDoPedido avisos,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _perfis = perfis;
        _avisos = avisos;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    internal async Task<(Result Resposta, IEnumerable<object> Mensagens)> CancelarAsync(
        Guid pedidoId, Identidade solicitante, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await PedidoDoSolicitante
            .ObterParaAlterarAsync(_pedidos, pedidoId, solicitante, cancellationToken)
            .ConfigureAwait(false);

        return pedido is null
            ? (Result.Failure(PedidoHandlerErros.NaoEncontrado()), [])
            : await CancelarPedidoAsync(pedido, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Result Resposta, IEnumerable<object> Mensagens)> CancelarPedidoAsync(
        PedidoDeConcessao pedido, CancellationToken cancellationToken)
    {
        SituacaoDoPedido situacaoAnterior = pedido.Andamento.Situacao;
        Result cancelado = pedido.Cancelar(_clock.GetUtcNow());
        if (cancelado.IsFailure)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return (cancelado, []);
        }

        await _unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return (Result.Success(), await AvisarAsync(pedido, situacaoAnterior, cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<EnviarEmail>> AvisarAsync(
        PedidoDeConcessao pedido, SituacaoDoPedido situacaoAnterior, CancellationToken cancellationToken)
    {
        Perfil? perfil = await _perfis.ObterPorIdAsync(pedido.PerfilId, cancellationToken).ConfigureAwait(false);

        return await _avisos
            .AvisarAsync(pedido, perfil?.Nome ?? string.Empty, situacaoAnterior, cancellationToken)
            .ConfigureAwait(false);
    }
}
