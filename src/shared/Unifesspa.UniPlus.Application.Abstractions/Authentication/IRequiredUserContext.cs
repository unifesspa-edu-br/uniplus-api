namespace Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Variante não-nullable de <see cref="IUserContext"/> para handlers e
/// controllers protegidos por <c>[Authorize]</c> onde anônimo é estado
/// impossível. Acessar qualquer propriedade quando
/// <see cref="IUserContext.IsAuthenticated"/> for <see langword="false"/>
/// resulta em <see cref="InvalidOperationException"/> — fail-fast em caso de
/// configuração inconsistente (atributo de auth ausente, middleware fora de
/// ordem) em vez de <c>NullReferenceException</c> ou silently fallback.
/// </summary>
public interface IRequiredUserContext
{
    /// <summary>Identificador do principal (sub claim do JWT).</summary>
    string UserId { get; }

    /// <summary>Display name do usuário.</summary>
    string Name { get; }

    /// <summary>Email do usuário (do escopo OIDC).</summary>
    string Email { get; }

    /// <summary>Roles do usuário (realm + resource).</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>Verifica se o usuário tem o role indicado.</summary>
    bool HasRole(string role);
}
