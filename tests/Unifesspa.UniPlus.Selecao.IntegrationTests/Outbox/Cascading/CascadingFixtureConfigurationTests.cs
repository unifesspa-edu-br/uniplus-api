namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Sentinela de configuração efetiva da <see cref="CascadingFixture"/>
/// (issue #197). Valida que o host runtime enxerga:
/// <list type="bullet">
///   <item><description>A connection string do Postgres do Testcontainers em
///   <c>ConnectionStrings:SelecaoDb</c>;</description></item>
///   <item><description><c>Kafka:BootstrapServers</c> em whitespace, garantindo que
///   o Wolverine NÃO tente abrir transporte Kafka contra <c>localhost:9092</c>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// O teste pega o <see cref="IConfiguration"/> da factory após o build do host —
/// é o mesmo objeto usado por <c>UseWolverineOutboxCascading</c> e
/// <c>SelecaoInfrastructureRegistration.AddSelecaoInfrastructure</c>. Se a
/// configuração efetiva divergir da intenção da fixture, o teste falha cedo
/// com mensagem direcional.
/// </remarks>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class CascadingFixtureConfigurationTests
{
    private readonly CascadingFixture _fixture;

    public CascadingFixtureConfigurationTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "IConfiguration efetiva expõe a connection string do testcontainer Postgres")]
    public void Configuration_ExpoeConnectionStringDoTestcontainer()
    {
        IConfiguration configuration = _fixture.Factory.Services.GetRequiredService<IConfiguration>();

        string? connectionString = configuration.GetConnectionString("SelecaoDb");

        connectionString.Should().NotBeNullOrWhiteSpace(
            because: "ConnectionStrings__SelecaoDb deve ter sido injetada pela fixture via env var; "
            + "se ficou vazia, AddSelecaoInfrastructure não conseguiria configurar o DbContext.");
        // Sem expor a connection string completa em logs/asserts — apenas
        // validar marcadores estruturais (host/Database). PostgreSQL connection
        // string típica do Testcontainers contém Host=localhost;Port=<random>;Database=uniplus_outbox_cascading.
        connectionString.Should().Contain("uniplus_outbox_cascading",
            because: "a fixture cria o testcontainer com WithDatabase(\"uniplus_outbox_cascading\"); "
            + "se o nome do database divergir, a config efetiva não está vindo do testcontainer.");
    }

    [Fact(DisplayName = "IConfiguration efetiva mantém Kafka:BootstrapServers em whitespace para desligar o transporte")]
    public void Configuration_KafkaBootstrapServers_EmWhitespace()
    {
        IConfiguration configuration = _fixture.Factory.Services.GetRequiredService<IConfiguration>();

        string? bootstrapServers = configuration["Kafka:BootstrapServers"];

        bootstrapServers.Should().NotBeNull(
            "a fixture seta a env var explicitamente; null aqui significa que o appsettings.Development venceu.");
        string.IsNullOrWhiteSpace(bootstrapServers).Should().BeTrue(
            because: "se Kafka:BootstrapServers tiver valor real (ex.: \"localhost:9092\" do appsettings.Development), "
            + "o Wolverine inicializaria o transporte Kafka e ficaria em retry indefinido — a fixture não provisiona "
            + "broker Kafka, então o transporte tem que estar desligado.");
    }
}
