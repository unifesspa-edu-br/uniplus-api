namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Stub mínimo de <see cref="IUserContext"/> para testes de persistência da
/// Story #586. Suporta apenas o caminho "autenticado com <c>sub</c> fixo"
/// usado pelos interceptors (created_by, updated_by, deleted_by).
/// </summary>
internal sealed class StubUserContext : IUserContext
{
    public StubUserContext(string userId)
    {
        UserId = userId;
    }

    public bool IsAuthenticated => true;
    public string? UserId { get; }
    public string? Name => null;
    public string? Email => null;
    public string? Cpf => null;
    public string? NomeSocial => null;
    public IReadOnlyList<string> Roles => [];
    public bool HasRole(string role) => false;
    public IReadOnlyList<string> GetResourceRoles(string resourceName) => [];
    public bool IsPlataformaAdmin => false;
}
