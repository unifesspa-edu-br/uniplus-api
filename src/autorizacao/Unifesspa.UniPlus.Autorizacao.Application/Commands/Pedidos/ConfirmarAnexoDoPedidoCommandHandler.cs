namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// Grava o anexo como em verificação e devolve o pedido como mensagem de saída, que o Wolverine instala no outbox na
/// mesma transação: não há anexo em verificação sem pedido, nem pedido sem anexo (ADR-0146). A confirmação é de
/// <see cref="ConfirmacaoDoAnexoDoPedido"/>.
/// </summary>
public static class ConfirmarAnexoDoPedidoCommandHandler
{
    public static async Task<(Result<AnexoDoPedidoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ConfirmarAnexoDoPedidoCommand command,
        ConfirmacaoDoAnexoDoPedido confirmacao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(confirmacao);
        ArgumentNullException.ThrowIfNull(userContext);

        if (IdentidadeDoToken.De(userContext) is not { } solicitante)
        {
            return (Result<AnexoDoPedidoDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado()), []);
        }

        Result<AnexoDoPedido> confirmado = await confirmacao.ConfirmarAsync(command, solicitante, cancellationToken).ConfigureAwait(false);

        return confirmado.IsFailure
            ? (Result<AnexoDoPedidoDto>.ValidationFailure(confirmado.Errors), [])
            : (Result<AnexoDoPedidoDto>.Success(PedidoMapping.ToDto(confirmado.Value!)), [PedidoAoVerificador(confirmado.Value!)]);
    }

    private static PedidoDeVerificacaoDeArquivo PedidoAoVerificador(AnexoDoPedido anexo) =>
        new(
            anexo.Id,
            anexo.ChaveDeEnvio,
            anexo.ETag!,
            anexo.ChaveDefinitiva,
            FormatoDoArquivo.Pdf,
            anexo.TamanhoDeclarado,
            DestinoDoResultado.Autorizacao);
}
