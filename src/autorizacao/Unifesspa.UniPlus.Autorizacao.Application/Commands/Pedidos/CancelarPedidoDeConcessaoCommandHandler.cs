namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do cancelamento. O pedido de outra pessoa não se distingue do inexistente. O endpoint não leva
/// <c>Idempotency-Key</c>: o conflito de concorrência propaga e o middleware o responde como 409 (ADR-0119). O aviso ao
/// beneficiário sai como mensagem de saída, no outbox da mesma transação.
/// </summary>
public static class CancelarPedidoDeConcessaoCommandHandler
{
    public static async Task<(Result Resposta, IEnumerable<object> Mensagens)> Handle(
        CancelarPedidoDeConcessaoCommand command,
        IPedidoDeConcessaoRepository repository,
        IPerfilRepository perfis,
        AvisosDoPedido avisos,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(perfis);
        ArgumentNullException.ThrowIfNull(avisos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.De(userContext) is not { } solicitante)
        {
            return Recusa(PedidoHandlerErros.AutorNaoIdentificado());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaAlterarAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (pedido is null || pedido.Solicitante != solicitante)
        {
            return Recusa(PedidoHandlerErros.NaoEncontrado());
        }

        SituacaoDoPedido situacaoAnterior = pedido.Situacao;
        Result cancelado = pedido.Cancelar(timeProvider.GetUtcNow());
        if (cancelado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return (cancelado, []);
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        Perfil? perfil = await perfis.ObterPorIdAsync(pedido.PerfilId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<EnviarEmail> mensagens = await avisos
            .AvisarAsync(pedido, perfil?.Nome ?? string.Empty, situacaoAnterior, cancellationToken)
            .ConfigureAwait(false);

        return (Result.Success(), mensagens);
    }

    private static (Result Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result.Failure(erro), []);
}
