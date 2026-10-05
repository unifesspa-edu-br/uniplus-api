namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento.AcervoPublico;

using System.Diagnostics.CodeAnalysis;

// DisableParallelization: a fixture escreve as connection strings em variáveis de ambiente do
// processo, como a das demais suítes da API com Postgres.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convention name for xUnit collection definitions ends with 'Collection'.")]
public sealed class AcervoPublicoE2ECollection : ICollectionFixture<AcervoPublicoE2EFixture>
{
    public const string Name = "acervo-publico-e2e";
}
