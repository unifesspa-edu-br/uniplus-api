namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox;

using System.Diagnostics.CodeAnalysis;

[CollectionDefinition(Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit convention names collection definition classes <Name>Collection paired with [CollectionDefinition].")]
public sealed class SelecaoOutboxCollection : ICollectionFixture<SelecaoOutboxFixture>
{
    public const string Name = "Selecao Outbox";
}
