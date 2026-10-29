namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Grava o anexo como em verificação e devolve o pedido como mensagem de saída, que o Wolverine instala no outbox na
/// mesma transação: não há anexo em verificação sem pedido, nem pedido sem anexo (ADR-0146).
/// </summary>
public static class ConfirmarAnexoDoPedidoCommandHandler
{
    public static async Task<(Result<AnexoDoPedidoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarAnexoDoPedidoCommand command,
        IPedidoDeConcessaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.De(userContext) is not { } solicitante)
        {
            return Recusa(PedidoHandlerErros.AutorNaoIdentificado());
        }

        PedidoDeConcessao? pedido = await repository.ObterParaAlterarAsync(command.PedidoId, cancellationToken).ConfigureAwait(false);
        if (pedido is null || pedido.Solicitante != solicitante)
        {
            return Recusa(PedidoHandlerErros.NaoEncontrado());
        }

        Result<AnexoDoPedido> confirmado = pedido.ConfirmarAnexo(command.AnexoId, command.ETag, timeProvider.GetUtcNow());
        if (confirmado.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return (Result<AnexoDoPedidoDto>.ValidationFailure(confirmado.Errors), []);
        }

        // Marca a linha do pedido: confirmar o anexo e decidir o pedido não correm juntos.
        repository.Atualizar(pedido);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Recusa(PedidoHandlerErros.ConflitoDeConcorrencia());
        }

        AnexoDoPedido anexo = confirmado.Value!;
        PedidoDeVerificacaoDeArquivo pedidoAoVerificador = new(
            anexo.Id,
            anexo.ChaveDeEnvio,
            anexo.ETag!,
            anexo.ChaveDefinitiva,
            FormatoDoArquivo.Pdf,
            anexo.TamanhoDeclarado,
            DestinoDoResultado.Autorizacao);

        return (Result<AnexoDoPedidoDto>.Success(PedidoMapping.ToDto(anexo)), [pedidoAoVerificador]);
    }

    private static (Result<AnexoDoPedidoDto> Resposta, IEnumerable<object> Mensagens) Recusa(DomainError erro) =>
        (Result<AnexoDoPedidoDto>.Failure(erro), []);
}
