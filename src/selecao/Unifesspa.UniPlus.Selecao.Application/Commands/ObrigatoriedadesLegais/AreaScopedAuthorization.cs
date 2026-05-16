namespace Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Verificação imperativa de RBAC área-scoped (ADR-0057) ao nível de handler.
/// Replica a lógica do <c>AreaScopedAuthorizationHandler</c> de
/// <c>Infrastructure.Core/Authorization</c> em forma <c>Result&lt;T&gt;</c>
/// — Application não depende de <c>IAuthorizationService</c>/<c>HttpContext</c>.
/// </summary>
internal static class AreaScopedAuthorization
{
    /// <summary>
    /// Autoriza a operação sobre a regra dado o <paramref name="proprietario"/>
    /// dela. <c>plataforma-admin</c> sempre passa (ADR-0057 §"bypass
    /// platform-wide"); admin da área dona passa; demais casos falham com
    /// <c>Area.EscopoNegado</c>.
    /// </summary>
    public static Result Autorizar(IUserContext userContext, AreaCodigo? proprietario)
    {
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.IsPlataformaAdmin)
        {
            return Result.Success();
        }

        if (proprietario is { } prop && userContext.AreasAdministradas.Contains(prop))
        {
            return Result.Success();
        }

        return Result.Failure(new DomainError(
            "Area.EscopoNegado",
            "O caller não administra a área proprietária do recurso e não é plataforma-admin."));
    }
}
