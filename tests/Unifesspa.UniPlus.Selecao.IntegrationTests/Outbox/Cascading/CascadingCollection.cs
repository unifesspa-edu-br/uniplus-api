namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

// DisableParallelization=true protege a env var ConnectionStrings__SelecaoDb
// (setada pela CascadingFixture) contra interleaving com OUTRAS coleções de
// teste rodando em paralelo no mesmo processo. Não desabilita paralelismo
// global — somente impede que esta coleção rode simultaneamente com qualquer
// outra. Tests dentro desta coleção já são serializados pelo ICollectionFixture.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convention name for xUnit collection definitions ends with 'Collection'.")]
public sealed class CascadingCollection : ICollectionFixture<CascadingFixture>
{
    public const string Name = "outbox-cascading";
}
