namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler da criação: a conferência é de <see cref="ConferenciaDoNovoPedido"/>, e a gravação com os avisos, de
/// <see cref="GravacaoDoNovoPedido"/>. A janela de migração vencida e ainda não materializada é materializada antes da
/// trava do perfil.
/// </summary>
public static class CriarPedidoDeConcessaoCommandHandler
{
    public static Task Before(CriarPedidoDeConcessaoCommand command, MaterializacaoPrevia previa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(previa);

        return previa.GarantirAsync(command.PerfilId, cancellationToken);
    }

    public static async Task<(Result<PedidoDeConcessaoDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        CriarPedidoDeConcessaoCommand command,
        ConferenciaDoNovoPedido conferencia,
        GravacaoDoNovoPedido gravacao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(conferencia);
        ArgumentNullException.ThrowIfNull(gravacao);
        ArgumentNullException.ThrowIfNull(userContext);
        if (IdentidadeDoToken.PessoaDe(userContext) is not { } solicitante)
        {
            return (Result<PedidoDeConcessaoDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado()), []);
        }

        Result<NovoPedido> conferido = await conferencia.ConferirAsync(command, solicitante, cancellationToken).ConfigureAwait(false);

        return conferido.IsFailure
            ? (Result<PedidoDeConcessaoDto>.ValidationFailure(conferido.Errors), [])
            : await gravacao.GravarAsync(conferido.Value!, cancellationToken).ConfigureAwait(false);
    }
}
