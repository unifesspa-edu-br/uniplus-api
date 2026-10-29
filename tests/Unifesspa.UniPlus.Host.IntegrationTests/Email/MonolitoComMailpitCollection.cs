namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;

using System.Diagnostics.CodeAnalysis;

[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit CollectionDefinition exige tipo público.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class MonolitoComMailpitCollection : ICollectionFixture<MonolitoComMailpitFixture>
{
    public const string Name = "Monolito com Mailpit";
}
