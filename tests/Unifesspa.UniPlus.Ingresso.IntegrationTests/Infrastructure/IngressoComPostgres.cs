namespace Unifesspa.UniPlus.Ingresso.IntegrationTests.Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

// DisableParallelization=true protege a env var ConnectionStrings__UniPlusDb
// contra interleaving com outras coleções de teste no mesmo processo.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IngressoComPostgres : ICollectionFixture<MonolitoPostgresFixture>
{
    public const string Name = "ingresso-postgres";
}
