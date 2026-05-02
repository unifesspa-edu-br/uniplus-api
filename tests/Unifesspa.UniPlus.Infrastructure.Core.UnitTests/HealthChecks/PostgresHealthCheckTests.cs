namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.HealthChecks;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.HealthChecks;

public sealed class PostgresHealthCheckTests
{
    [Fact]
    public void Construtor_DadoConnectionStringValida_NaoDeveLancarExcecao()
    {
        const string connectionString = "Host=localhost;Database=uniplus;Username=app;Password=secret";

        Action acao = () => _ = new PostgresHealthCheck(connectionString);

        acao.Should().NotThrow();
    }

    [Fact]
    public void Construtor_DadoConnectionStringVazia_NaoDeveLancarExcecao()
    {
        Action acao = () => _ = new PostgresHealthCheck(string.Empty);

        acao.Should().NotThrow();
    }
}
