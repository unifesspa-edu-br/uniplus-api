namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

// DisableParallelization=true protege a env var ConnectionStrings__ConfiguracaoDb
// contra interleaving com outras coleções de teste no mesmo processo.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class ConfiguracaoEndpointCollection : ICollectionFixture<ConfiguracaoEndpointFixture>
{
    public const string Name = "configuracao-endpoint";
}
