namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Coleção xUnit que compartilha a <see cref="MonolitoHostFixture"/> entre as
/// suítes do host. <c>DisableParallelization=true</c> protege as env vars
/// process-wide das connection strings contra interleaving com outras coleções
/// no mesmo processo de teste.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit CollectionDefinition exige tipo público.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class MonolitoHostCollection : ICollectionFixture<MonolitoHostFixture>
{
    public const string Name = "Monolito Host";
}
