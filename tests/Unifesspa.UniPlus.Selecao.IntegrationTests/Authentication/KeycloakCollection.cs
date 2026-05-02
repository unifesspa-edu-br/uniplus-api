namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Authentication;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// xUnit collection que serializa a execução das classes de teste E2E que precisam do Keycloak real,
/// reaproveitando uma única instância da <see cref="KeycloakContainerFixture"/> entre elas e mantendo
/// o cold start pago apenas uma vez por execução. xUnit exige que a <c>[CollectionDefinition]</c> viva
/// no mesmo assembly dos testes que a usam — por isso esta classe vive em Selecao.IntegrationTests
/// (e não em IntegrationTests.Fixtures).
/// </summary>
[CollectionDefinition(KeycloakContainerFixture.CollectionName)]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit: o sufixo 'Collection' identifica a classe como CollectionDefinition.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x exige que CollectionDefinitions sejam públicas para que o runner as descubra.")]
public sealed class KeycloakCollection : ICollectionFixture<KeycloakContainerFixture>
{
}
