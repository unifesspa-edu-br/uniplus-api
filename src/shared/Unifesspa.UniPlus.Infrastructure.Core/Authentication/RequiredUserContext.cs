namespace Unifesspa.UniPlus.Infrastructure.Core.Authentication;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Adapter que projeta <see cref="IUserContext"/> em
/// <see cref="IRequiredUserContext"/>: lê do mesmo backend (HttpContext claims)
/// mas falha rapidamente com <see cref="InvalidOperationException"/> se o
/// principal não estiver autenticado, em vez de propagar nullables. Use em
/// handlers/controllers protegidos por <c>[Authorize]</c>.
/// </summary>
internal sealed class RequiredUserContext : IRequiredUserContext
{
    private readonly IUserContext _inner;

    public RequiredUserContext(IUserContext inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public string UserId => Require(_inner.UserId, nameof(UserId));

    public string Name => Require(_inner.Name, nameof(Name));

    public string Email => Require(_inner.Email, nameof(Email));

    public IReadOnlyList<string> Roles
    {
        get
        {
            EnsureAuthenticated();
            return _inner.Roles;
        }
    }

    public bool HasRole(string role)
    {
        EnsureAuthenticated();
        return _inner.HasRole(role);
    }

    private void EnsureAuthenticated()
    {
        if (!_inner.IsAuthenticated)
            throw new InvalidOperationException(
                "IRequiredUserContext acessado em request anônima. Verifique se o endpoint tem [Authorize] e se o middleware de autenticação está antes do MVC.");
    }

    private string Require(string? value, string field)
    {
        EnsureAuthenticated();
        // IsNullOrWhiteSpace defensivamente — HttpUserContext já filtra
        // whitespace na fonte, mas IUserContext custom poderia retornar
        // " " e isso violaria o contrato semântico de "claim presente".
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Claim '{field}' ausente no token JWT validado. Verifique a configuração do IdP e os escopos OIDC solicitados.");
        return value;
    }
}
