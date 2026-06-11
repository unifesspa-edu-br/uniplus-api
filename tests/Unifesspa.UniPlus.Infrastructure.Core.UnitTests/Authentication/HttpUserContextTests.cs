namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authentication;

using System.Security.Claims;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Infrastructure.Core.Authentication;

public sealed class HttpUserContextTests
{
    [Fact]
    public void UserId_Should_ReturnSubClaim_WhenPresent()
    {
        HttpUserContext context = CreateContext(
            new Claim("sub", "user-42"));

        context.UserId.Should().Be("user-42");
    }

    [Fact]
    public void Name_Should_PreferClaimTypesName_OverPreferredUsername()
    {
        HttpUserContext context = CreateContext(
            new Claim("preferred_username", "usuario.teste"),
            new Claim(ClaimTypes.Name, "Maria"));

        context.Name.Should().Be("Maria");
    }

    [Fact]
    public void Name_Should_FallBackToPreferredUsername_WhenClaimTypesNameAbsent()
    {
        HttpUserContext context = CreateContext(
            new Claim("preferred_username", "usuario.teste"));

        context.Name.Should().Be("usuario.teste");
    }

    [Fact]
    public void Roles_Should_ResolveFromRealmAccess()
    {
        HttpUserContext context = CreateContext(
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { "admin", "gestor" } })));

        context.Roles.Should().BeEquivalentTo(["admin", "gestor"]);
    }

    [Fact]
    public void Roles_Should_MergeRealmAccessAndDirectClaims_WithoutDuplicates()
    {
        HttpUserContext context = CreateContext(
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("roles", "avaliador"),
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { "admin", "gestor" } })));

        context.Roles.Should().BeEquivalentTo(["admin", "avaliador", "gestor"]);
    }

    [Fact]
    public void Roles_Should_ReturnEmpty_WhenRealmAccessIsMalformed()
    {
        HttpUserContext context = CreateContext(
            new Claim("realm_access", "{ this isn't valid json"));

        context.Roles.Should().BeEmpty();
    }

    [Fact]
    public void Roles_Should_SkipNonStringElements_WithoutThrowing()
    {
        // realm_access.roles com elementos não-string (número, objeto) é
        // misconfiguração de IdP — deve ser ignorado, não virar 500.
        HttpUserContext context = CreateContext(
            new Claim("realm_access", """{ "roles": ["gestor", 123, { "x": 1 }, "admin"] }"""));

        context.Roles.Should().BeEquivalentTo(["gestor", "admin"]);
    }

    [Fact]
    public void HasRole_Should_BeCaseInsensitive()
    {
        HttpUserContext context = CreateContext(
            new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { "Admin" } })));

        context.HasRole("admin").Should().BeTrue();
        context.HasRole("ADMIN").Should().BeTrue();
        context.HasRole("gestor").Should().BeFalse();
    }

    [Fact]
    public void Cpf_Should_ReadUniPlusClaim_WhenPresent()
    {
        HttpUserContext context = CreateContext(
            new Claim("cpf", "529.982.247-25"));

        context.Cpf.Should().Be("529.982.247-25");
    }

    [Fact]
    public void Cpf_Should_ReturnNull_WhenClaimAbsent()
    {
        HttpUserContext context = CreateContext(
            new Claim("sub", "user-42"));

        context.Cpf.Should().BeNull();
    }

    [Fact]
    public void NomeSocial_Should_ReadUniPlusClaim_WhenPresent()
    {
        HttpUserContext context = CreateContext(
            new Claim("nomeSocial", "Maria dos Santos"));

        context.NomeSocial.Should().Be("Maria dos Santos");
    }

    [Fact]
    public void NomeSocial_Should_ReturnNull_WhenClaimAbsent()
    {
        HttpUserContext context = CreateContext(
            new Claim("sub", "user-42"));

        context.NomeSocial.Should().BeNull();
    }

    [Fact]
    public void GetResourceRoles_Should_ReturnRolesForNamedResource()
    {
        string resourceAccess = JsonSerializer.Serialize(new
        {
            uniplus = new { roles = new[] { "portal-admin" } },
            outro = new { roles = new[] { "irrelevante" } },
        });

        HttpUserContext context = CreateContext(
            new Claim("resource_access", resourceAccess));

        context.GetResourceRoles("uniplus").Should().BeEquivalentTo(["portal-admin"]);
        context.GetResourceRoles("inexistente").Should().BeEmpty();
    }

    [Fact]
    public void GetResourceRoles_Should_ReturnEmpty_WhenResourceAccessIsMalformed()
    {
        HttpUserContext context = CreateContext(
            new Claim("resource_access", "not-json"));

        context.GetResourceRoles("uniplus").Should().BeEmpty();
    }

    [Fact]
    public void GetResourceRoles_Should_CacheResultByResourceName()
    {
        string resourceAccess = JsonSerializer.Serialize(new
        {
            uniplus = new { roles = new[] { "portal-admin" } },
        });

        HttpUserContext context = CreateContext(
            new Claim("resource_access", resourceAccess));

        IReadOnlyList<string> first = context.GetResourceRoles("uniplus");
        IReadOnlyList<string> second = context.GetResourceRoles("uniplus");

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AnonymousUser_Should_ReturnNullOrEmpty_ForAllAccessors()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        HttpUserContext context = new(accessor, NullLogger<HttpUserContext>.Instance);

        context.UserId.Should().BeNull();
        context.Name.Should().BeNull();
        context.Email.Should().BeNull();
        context.Cpf.Should().BeNull();
        context.NomeSocial.Should().BeNull();
        context.Roles.Should().BeEmpty();
        context.GetResourceRoles("uniplus").Should().BeEmpty();
    }

    [Fact]
    public void IsAuthenticated_Should_ReturnTrue_WhenClaimsIdentityHasAuthenticationType()
    {
        // ClaimsIdentity construída com authentication type não-null/não-empty
        // tem IsAuthenticated == true (.NET BCL).
        HttpUserContext context = CreateContext(new Claim("sub", "user-1"));

        context.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_Should_ReturnFalse_WhenPrincipalIsAnonymous()
    {
        ClaimsIdentity anonymous = new();
        ClaimsPrincipal principal = new(anonymous);
        DefaultHttpContext httpContext = new() { User = principal };
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        HttpUserContext context = new(accessor, NullLogger<HttpUserContext>.Instance);

        context.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_Should_ReturnFalse_WhenHttpContextIsNull()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        HttpUserContext context = new(accessor, NullLogger<HttpUserContext>.Instance);

        context.IsAuthenticated.Should().BeFalse();
    }

    // ─── IsPlataformaAdmin ─────────────────────────────────────────────────

    [Fact]
    public void IsPlataformaAdmin_Should_ReturnTrue_WhenPlataformaAdminRolePresent()
    {
        HttpUserContext context = CreateContext(RealmRoles("plataforma-admin"));

        context.IsPlataformaAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsPlataformaAdmin_Should_ReturnFalse_WhenRoleAbsent()
    {
        HttpUserContext context = CreateContext(RealmRoles("candidato"));

        context.IsPlataformaAdmin.Should().BeFalse();
    }

    [Fact]
    public void IsPlataformaAdmin_Should_ReturnFalse_WhenNoRealmAccessClaim()
    {
        HttpUserContext context = CreateContext(new Claim("sub", "user-42"));

        context.IsPlataformaAdmin.Should().BeFalse();
    }

    [Fact]
    public void IsPlataformaAdmin_Should_ReturnFalse_ForAnonymousUser()
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        HttpUserContext context = new(accessor, NullLogger<HttpUserContext>.Instance);

        context.IsPlataformaAdmin.Should().BeFalse();
    }

    private static Claim RealmRoles(params string[] roles) =>
        new("realm_access", JsonSerializer.Serialize(new { roles }));

    private static HttpUserContext CreateContext(params Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, "Test");
        ClaimsPrincipal principal = new(identity);

        DefaultHttpContext httpContext = new() { User = principal };
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        return new HttpUserContext(accessor, NullLogger<HttpUserContext>.Instance);
    }
}
