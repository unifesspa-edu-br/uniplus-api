namespace Unifesspa.UniPlus.Infrastructure.Core.Profile;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Extension methods that map shared profile endpoints onto the application pipeline.
/// </summary>
public static class ProfileEndpointsExtensions
{
    /// <summary>
    /// Maps the shared <c>/api/profile</c> endpoints (currently the authenticated-user
    /// profile <c>/me</c>, exposing CPF, NomeSocial and the granted access profiles).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public static IEndpointRouteBuilder MapSharedProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder profileGroup = endpoints.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        profileGroup.MapGet("/me", ObterPerfilAsync)
            .WithName("GetAuthenticatedUserProfile")
            .WithSummary("Retorna o perfil do usuário autenticado")
            .WithDescription("Retorna a identidade do access token (nome, e-mail, CPF, NomeSocial) e os perfis de acesso que a pessoa tem por concessão vigente, cada um com o escopo (unidade ou instituição) e o prazo. O token identifica a pessoa e não concede acesso. Requer autenticação.")
            .Produces<UserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> ObterPerfilAsync(
        IUserContext userContext,
        IConcessoesAtivasDoSujeito concessoes,
        IUnidadeReader unidades,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        DateTimeOffset agora = clock.GetUtcNow();

        Result<IReadOnlyList<PerfilConcedidoResponse>> perfis = await PerfisConcedidosDoUsuario
            .ListarAsync(userContext.Issuer, userContext.UserId, concessoes, unidades, agora, cancellationToken)
            .ConfigureAwait(false);

        // Sem emissor ou subject a identidade do token é incompleta: é falha de autenticação, não de acesso.
        if (perfis.IsFailure)
        {
            return Results.Challenge();
        }

        return Results.Ok(new UserProfileResponse(
            userContext.UserId,
            userContext.Name,
            userContext.Email,
            userContext.Cpf,
            userContext.NomeSocial,
            perfis.Value!,
            agora));
    }
}
