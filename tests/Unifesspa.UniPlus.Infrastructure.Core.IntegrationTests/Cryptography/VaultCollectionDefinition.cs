namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Cryptography;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// xUnit collection que serializa a execução das classes de teste que precisam do Vault real,
/// reaproveitando uma única instância da <see cref="VaultContainerFixture"/> entre elas.
/// xUnit exige que a <c>[CollectionDefinition]</c> viva no mesmo assembly dos testes que a usam.
/// </summary>
[CollectionDefinition(VaultContainerFixture.CollectionName)]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit: o sufixo 'Collection' identifica a classe como CollectionDefinition.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x exige que CollectionDefinitions sejam públicas para que o runner as descubra.")]
public sealed class VaultCollection : ICollectionFixture<VaultContainerFixture>
{
}
