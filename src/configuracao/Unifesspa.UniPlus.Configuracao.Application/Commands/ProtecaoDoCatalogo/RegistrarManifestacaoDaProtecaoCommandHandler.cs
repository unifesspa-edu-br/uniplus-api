namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Registra a manifestação técnica sobre a proteção de um tipo de documento ou fato do candidato. A forma do comando é
/// conferida antes de qualquer leitura (ADR-0125); a permissão de se manifestar é do endpoint.
/// </summary>
public static class RegistrarManifestacaoDaProtecaoCommandHandler
{
    public static async Task<Result> Handle(
        RegistrarManifestacaoDaProtecaoCommand command,
        ManifestacaoNaProtecaoDoCatalogo manifestacao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestacao);

        Result<PedidoDeManifestacao> pedido = PedidoDeManifestacao.Ler(command);
        return pedido.IsFailure
            ? Result.ValidationFailure(pedido.Errors)
            : await manifestacao.RegistrarAsync(pedido.Value!, AtorDoToken.De(userContext), cancellationToken).ConfigureAwait(false);
    }
}
