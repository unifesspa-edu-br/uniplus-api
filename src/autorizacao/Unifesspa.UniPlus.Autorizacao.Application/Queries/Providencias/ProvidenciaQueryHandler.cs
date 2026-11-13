namespace Unifesspa.UniPlus.Autorizacao.Application.Queries.Providencias;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Os handlers da leitura das providências; a leitura em si é de <see cref="LeituraDaProvidencia"/>.</summary>
public static class ProvidenciaQueryHandler
{
    public static Task<Result<ProvidenciaDto>> Handle(
        ObterProvidenciaQuery query, LeituraDaProvidencia leitura, IVerificadorDeAcesso verificador, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(userContext);

        return leitura.ObterAsync(query.Id, verificador, IdentidadeDoToken.De(userContext), cancellationToken);
    }

    public static Task<ListarProvidenciasResult> Handle(
        ListarProvidenciasQuery query, LeituraDaProvidencia leitura, IVerificadorDeAcesso verificador, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(leitura);
        ArgumentNullException.ThrowIfNull(userContext);

        return leitura.ListarAsync(query, verificador, IdentidadeDoToken.De(userContext), cancellationToken);
    }
}
