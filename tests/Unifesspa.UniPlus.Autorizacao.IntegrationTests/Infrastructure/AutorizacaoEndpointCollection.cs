namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

// DisableParallelization protege as variáveis de ambiente de connection string contra
// interleaving com outras coleções de teste no mesmo processo.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class AutorizacaoEndpointCollection : ICollectionFixture<AutorizacaoEndpointFixture>
{
    public const string Name = "autorizacao-endpoint";
}
