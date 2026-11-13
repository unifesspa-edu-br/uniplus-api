namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Aplica a decisão da proteção de dados ao tipo de documento ou ao fato. A forma do comando é conferida antes de
/// qualquer leitura (ADR-0125); a permissão de aprovar é do endpoint, e o item que não está numa solicitação é recusado
/// pelo próprio circuito do item.
/// </summary>
public static class DecidirProtecaoDoCatalogoCommandHandler
{
    public static async Task<Result> Handle(
        DecidirProtecaoDoCatalogoCommand command,
        DecisaoNaProtecaoDoCatalogo decisao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decisao);

        Result<PedidoDeDecisao> pedido = PedidoDeDecisao.Ler(command);
        return pedido.IsFailure
            ? Result.ValidationFailure(pedido.Errors)
            : await decisao.DecidirAsync(pedido.Value!, AtorDoToken.De(userContext), cancellationToken).ConfigureAwait(false);
    }
}
