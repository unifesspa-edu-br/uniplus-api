namespace Unifesspa.UniPlus.Infrastructure.Core.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Handler de autorização baseada em recurso para <see cref="RequireAreaProprietarioRequirement"/>
/// (ADR-0057). Decide se o caller pode escrever num <c>IAreaScopedEntity</c>:
/// <list type="number">
///   <item>admin da área <c>Proprietario</c> do recurso — autoriza;</item>
///   <item><c>plataforma-admin</c> — autoriza (bypass platform-wide); quando o
///   item é área-scoped, emite um log operacional de edição on-behalf-of;</item>
///   <item>demais casos — nega (o 403 ProblemDetails vem da pipeline, ADR-0034).</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// O handler resolve o principal pelo <see cref="IUserContext"/> injetado — a
/// abstração canônica do usuário corrente do projeto (ADR-0033), request-scoped
/// e equivalente ao <c>HttpContext.User</c> — e não pelo
/// <c>AuthorizationHandlerContext.User</c>. No caminho canônico
/// (<c>IAuthorizationService.AuthorizeAsync(User, recurso, policy)</c>) os dois
/// são o mesmo principal; a policy <c>AreaScopedPolicies.RequireAreaProprietario</c>
/// inclui <c>RequireAuthenticatedUser()</c>, então uma request anônima é barrada
/// antes de chegar a este handler.
/// </para>
/// <para>
/// O log on-behalf-of é operacional, não a trilha de auditoria durável — a
/// tabela de auditoria do ADR-0057 entra com as entidades área-scoped (F2).
/// </para>
/// </remarks>
public sealed partial class AreaScopedAuthorizationHandler
    : AuthorizationHandler<RequireAreaProprietarioRequirement, IAreaScopedEntity>
{
    private readonly IUserContext _userContext;
    private readonly ILogger<AreaScopedAuthorizationHandler> _logger;

    public AreaScopedAuthorizationHandler(
        IUserContext userContext,
        ILogger<AreaScopedAuthorizationHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(logger);

        _userContext = userContext;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAreaProprietarioRequirement requirement,
        IAreaScopedEntity resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        AreaCodigo? proprietario = resource.Proprietario;

        // 1. Admin da área dona do recurso edita a própria entidade.
        if (proprietario is { } area && _userContext.AreasAdministradas.Contains(area))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 2. plataforma-admin: bypass platform-wide (ADR-0057). Quando o item é
        //    área-scoped, a edição é on-behalf-of e fica registrada em log.
        if (_userContext.IsPlataformaAdmin)
        {
            if (proprietario is { } areaOnBehalf)
            {
                LogEdicaoOnBehalfOf(
                    _logger,
                    _userContext.UserId ?? "desconhecido",
                    resource.GetType().Name,
                    areaOnBehalf.Value);
            }

            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 3. Demais casos — nega. Itens globais (Proprietario nulo) também caem
        //    aqui quando o caller não é plataforma-admin.
        context.Fail(new AuthorizationFailureReason(
            this,
            "O caller não administra a área proprietária do recurso e não é plataforma-admin."));
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Edição on-behalf-of: plataforma-admin {ActorSub} editou {ResourceType} da área {OnBehalfOfArea}")]
    private static partial void LogEdicaoOnBehalfOf(
        ILogger logger,
        string actorSub,
        string resourceType,
        string onBehalfOfArea);
}
