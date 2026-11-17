namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da criação do anexo; a emissão do envio é de <see cref="EmissaoDoEnvioDoAnexo"/>.</summary>
public static class CriarAnexoDoPedidoCommandHandler
{
    public static async Task<Result<AnexoParaEnvioDto>> Handle(
        CriarAnexoDoPedidoCommand command,
        EmissaoDoEnvioDoAnexo emissao,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(emissao);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.De(userContext) is { } solicitante
            ? await emissao.EmitirAsync(command, solicitante, cancellationToken).ConfigureAwait(false)
            : Result<AnexoParaEnvioDto>.Failure(PedidoHandlerErros.AutorNaoIdentificado());
    }
}
