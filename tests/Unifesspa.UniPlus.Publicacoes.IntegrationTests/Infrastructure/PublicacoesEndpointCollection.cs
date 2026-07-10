namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

// DisableParallelization=true protege as env vars ConnectionStrings__* — a fixture
// base as escreve process-wide — contra interleaving com outras coleções de teste.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class PublicacoesEndpointCollection : ICollectionFixture<PublicacoesEndpointFixture>
{
    public const string Name = "publicacoes-endpoint";
}
