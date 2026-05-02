namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Authentication;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// xUnit class fixture que segura uma única instância de <see cref="OidcRealApiFactory"/> ao longo
/// da execução da classe de testes. Necessário porque xUnit 2.x não suporta injetar uma collection
/// fixture (<see cref="KeycloakContainerFixture"/>) diretamente em uma class fixture; o wrapper
/// recebe a fixture pelo construtor do teste e a entrega ao factory na primeira chamada.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x exige que class fixtures sejam públicas para que o runner as instancie.")]
public sealed class OidcRealApiFactoryWrapper : IDisposable
{
    private readonly Lock _lock = new();
    private OidcRealApiFactory? _factory;

    public OidcRealApiFactory GetOrCreate(KeycloakContainerFixture keycloak)
    {
        ArgumentNullException.ThrowIfNull(keycloak);

        lock (_lock)
        {
            _factory ??= new OidcRealApiFactory(keycloak);
            return _factory;
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
