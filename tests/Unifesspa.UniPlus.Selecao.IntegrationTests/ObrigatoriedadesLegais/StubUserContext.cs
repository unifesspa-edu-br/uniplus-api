namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Stub mínimo de <see cref="IUserContext"/> para testes de persistência da
/// Story #460. Suporta apenas o caminho "autenticado com <c>sub</c> fixo"
/// usado pelos interceptors (snapshot_by, created_by). Os demais membros
/// expõem defaults seguros: as roles/áreas não influenciam o ciclo de save.
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
    public IReadOnlyCollection<AreaCodigo> AreasAdministradas => [];
    public bool IsPlataformaAdmin => false;
}
