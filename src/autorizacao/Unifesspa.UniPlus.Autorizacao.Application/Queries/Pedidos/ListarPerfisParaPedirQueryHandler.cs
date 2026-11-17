namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Pedidos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da lista de perfis que se podem pedir; a oferta é de <see cref="OfertaDePerfis"/>.</summary>
public static class ListarPerfisParaPedirQueryHandler
{
    public static async Task<Result<ListarPerfisParaPedirResult>> Handle(
        ListarPerfisParaPedirQuery query,
        OfertaDePerfis oferta,
        IVerificadorDeAcesso verificador,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(oferta);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.PessoaDe(userContext) is { } pessoa
            ? await oferta.ListarAsync(query, pessoa, verificador, cancellationToken).ConfigureAwait(false)
            : Result<ListarPerfisParaPedirResult>.Failure(PedidoHandlerErros.IdentidadeIncompleta());
    }
}
