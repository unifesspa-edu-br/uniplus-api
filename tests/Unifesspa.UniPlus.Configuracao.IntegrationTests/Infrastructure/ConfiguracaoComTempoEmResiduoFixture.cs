namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

/// <summary>
/// Postgres efêmero e a API UniPlus de <see cref="ConfiguracaoComTempoEmResiduoFactory"/>.
/// </summary>
public sealed class ConfiguracaoComTempoEmResiduoFixture : MonolitoPostgresFixtureBase<ConfiguracaoComTempoEmResiduoFactory>
{
    protected override ConfiguracaoComTempoEmResiduoFactory CreateFactory(string connectionString) => new(connectionString);
}
