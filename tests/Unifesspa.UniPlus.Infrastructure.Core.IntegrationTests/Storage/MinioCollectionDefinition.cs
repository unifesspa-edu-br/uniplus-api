namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Storage;

using System.Diagnostics.CodeAnalysis;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

[CollectionDefinition(MinioContainerFixture.CollectionName)]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção xUnit: o sufixo 'Collection' identifica a classe como CollectionDefinition.")]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit 2.x exige que CollectionDefinitions sejam públicas para que o runner as descubra.")]
public sealed class MinioCollection : ICollectionFixture<MinioContainerFixture>
{
}
