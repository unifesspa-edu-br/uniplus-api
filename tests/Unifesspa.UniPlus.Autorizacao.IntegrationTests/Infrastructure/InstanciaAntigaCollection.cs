namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

// Banco próprio: a suíte apaga o catálogo gravado para reproduzir o banco sem catálogo, o que as demais não toleram.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class InstanciaAntigaCollection : ICollectionFixture<AutorizacaoEndpointFixture>
{
    public const string Name = "autorizacao-instancia-antiga";
}
