namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Transforma o claim Keycloak <c>realm_access</c> (objeto JSON com array <c>roles</c>) em
/// claims individuais do tipo <see cref="ClaimTypes.Role"/>. Sem esta transformação,
/// <c>RequireRole("admin")</c> não funciona porque a validação padrão do .NET busca
/// <c>ClaimTypes.Role</c>, que o JWT do Keycloak não emite no formato esperado.
/// </summary>
/// <remarks>
/// <para>
/// Idempotente — se o principal já tem <c>ClaimTypes.Role</c> populado, a transformação não
/// adiciona duplicatas. Roda em todo request autenticado (cache em memória do principal por
/// request — performance é aceitável para o volume das APIs Uni+).
/// </para>
/// <para>
/// Compatibilidade: o claim <c>realm_access</c> é o formato canônico do Keycloak para realm
/// roles. Para resource-specific roles (<c>resource_access.{client}.roles</c>), usar
/// <see cref="HttpUserContext.GetResourceRoles"/> diretamente — não há equivalência 1:1 a
/// uma policy nativa do .NET.
/// </para>
/// </remarks>
public sealed class KeycloakRolesClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        Claim? realmAccess = identity.FindFirst(KeycloakClaims.RealmAccess);
        if (realmAccess is null || string.IsNullOrWhiteSpace(realmAccess.Value))
        {
            return Task.FromResult(principal);
        }

        string[] roles;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(realmAccess.Value);
            if (!doc.RootElement.TryGetProperty("roles", out JsonElement rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return Task.FromResult(principal);
            }

            roles = [.. rolesElement.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(role => !string.IsNullOrWhiteSpace(role))];
        }
        catch (JsonException)
        {
            // Claim malformado — provavelmente não-Keycloak. Retornar principal sem mutar.
            return Task.FromResult(principal);
        }

        if (roles.Length == 0)
        {
            return Task.FromResult(principal);
        }

        // Adiciona apenas roles que ainda não estão presentes — idempotente em caso de
        // re-execução da pipeline (ex.: cookie auth + redirecionamento).
        HashSet<string> existing = new(
            identity.FindAll(ClaimTypes.Role).Select(c => c.Value),
            StringComparer.Ordinal);

        foreach (string role in roles)
        {
            if (existing.Add(role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }
        }

        return Task.FromResult(principal);
    }
}
