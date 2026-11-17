namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A confirmação do envio do anexo: põe o anexo em verificação, com o ETag que o PUT devolveu. O pedido de outra pessoa não
/// se distingue do inexistente. O pedido é marcado antes da gravação: confirmar o anexo e decidir o pedido não correm juntos.
/// </summary>
public sealed class ConfirmacaoDoAnexoDoPedido
{
    private readonly IPedidoDeConcessaoRepository _pedidos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public ConfirmacaoDoAnexoDoPedido(IPedidoDeConcessaoRepository pedidos, IUnitOfWork unitOfWork, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _pedidos = pedidos;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    internal async Task<Result<AnexoDoPedido>> ConfirmarAsync(
        ConfirmarAnexoDoPedidoCommand command, Identidade solicitante, CancellationToken cancellationToken)
    {
        PedidoDeConcessao? pedido = await PedidoDoSolicitante
            .ObterParaAlterarAsync(_pedidos, command.PedidoId, solicitante, cancellationToken)
            .ConfigureAwait(false);

        return pedido is null
            ? Result<AnexoDoPedido>.Failure(PedidoHandlerErros.NaoEncontrado())
            : await ConfirmarDoPedidoAsync(pedido, command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<AnexoDoPedido>> ConfirmarDoPedidoAsync(
        PedidoDeConcessao pedido, ConfirmarAnexoDoPedidoCommand command, CancellationToken cancellationToken)
    {
        Result<AnexoDoPedido> confirmado = pedido.ConfirmarAnexo(command.AnexoId, command.ETag, _clock.GetUtcNow());
        if (confirmado.IsFailure)
        {
            _unitOfWork.DescartarAlteracoesNaoSalvas();
            return confirmado;
        }

        _pedidos.Atualizar(pedido);
        Result gravado = await GravacaoDoPedido.GravarAsync(_unitOfWork, cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure ? Result<AnexoDoPedido>.Failure(gravado.Error!) : confirmado;
    }
}
