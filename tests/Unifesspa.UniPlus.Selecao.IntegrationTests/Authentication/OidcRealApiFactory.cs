namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Authentication;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Factory de testes E2E que mantém o pipeline real <c>JwtBearer</c> ativo, apontando
/// <c>Auth:Authority</c> para o Keycloak provisionado pela <see cref="KeycloakContainerFixture"/>.
/// Diferentemente da <see cref="SelecaoApiFactory"/>, NÃO substitui o esquema de autenticação
/// produtivo — sobrescreve <see cref="ApiFactoryBase{TEntryPoint}.ConfigureTestAuthentication"/>
/// como no-op para que a validação real de issuer/audience/lifetime/signing key seja exercitada
/// contra o IdP em container.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x exige que fixtures e factories instanciados pelo runner sejam públicos.")]
public sealed class OidcRealApiFactory : ApiFactoryBase<Program>
{
    private readonly string _authority;

    public OidcRealApiFactory(KeycloakContainerFixture keycloak)
    {
        ArgumentNullException.ThrowIfNull(keycloak);
        _authority = keycloak.Authority;
    }

    /// <summary>
    /// No-op proposital: preserva o esquema <c>JwtBearer</c> registrado pela API em produção
    /// para que os testes E2E exercitem a cadeia real de validação contra o Keycloak.
    /// </summary>
    protected override void ConfigureTestAuthentication(IServiceCollection services)
    {
        // Intencionalmente vazio — ver docstring.
    }

    protected override IEnumerable<KeyValuePair<string, string?>> GetConfigurationOverrides() =>
    [
        new("ConnectionStrings:SelecaoDb", "Host=localhost;Port=5432;Database=uniplus_tests;Username=uniplus;Password=uniplus_dev"),
        new("Auth:Authority", _authority),
        new("Auth:Audience", KeycloakContainerFixture.Audience),
    ];
}
