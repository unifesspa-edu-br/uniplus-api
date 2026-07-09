namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Diagnostics.CodeAnalysis;

using Testcontainers.PostgreSql;

/// <summary>
/// Fixture base genérica que provisiona um Postgres efêmero (Testcontainers) com o
/// banco único <c>uniplus</c> e sobe a <b>API UniPlus</b> com Wolverine habilitado —
/// migrations on startup criam os 5 schemas de módulo e o Wolverine provisiona o
/// outbox (schema <c>wolverine</c>). É a base das suítes de endpoint dos módulos
/// (que agora rodam contra o monólito real).
/// </summary>
/// <typeparam name="TFactory">
/// Tipo concreto da factory. Permite que suítes especializem o composition root de
/// teste (ex.: <c>CascadingApiFactory</c> adiciona um coletor de domain events) sem
/// duplicar o ciclo de vida do container nem o gerenciamento das env vars. O default
/// não-genérico <see cref="MonolitoPostgresFixture"/> usa <see cref="MonolitoApiFactory"/>.
/// </typeparam>
/// <remarks>
/// As 5 connection strings são injetadas via env var (duplo underscore) <em>e</em>
/// via <c>ConfigureAppConfiguration</c> na factory, todas apontando para o mesmo
/// container. Suítes derivam esta fixture numa <c>[CollectionDefinition]</c> com
/// <c>DisableParallelization=true</c> para proteger as env vars process-wide.
/// </remarks>
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public abstract class MonolitoPostgresFixtureBase<TFactory> : IAsyncLifetime
    where TFactory : MonolitoApiFactory
{
    private static readonly string[] ConnectionStringEnvVars =
    [
        "ConnectionStrings__UniPlusDb",
        "ConnectionStrings__ConfiguracaoDb",
        "ConnectionStrings__OrganizacaoDb",
        "ConnectionStrings__SelecaoDb",
        "ConnectionStrings__IngressoDb",
        "ConnectionStrings__PublicacoesDb",
    ];

    private const string KafkaBootstrapEnvVar = "Kafka__BootstrapServers";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private readonly Dictionary<string, string?> _envVarsPrevios = [];
    private string? _kafkaEnvVarPrevio;
    private TFactory? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public TFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "Factory ainda não inicializada. InitializeAsync deve rodar antes do primeiro teste.");

    /// <summary>
    /// Cria a factory concreta apontando para o Postgres efêmero. Implementações
    /// devem habilitar o Wolverine (<c>wolverineEnabled: true</c>) — esta fixture
    /// existe justamente para exercitar migrations + outbox.
    /// </summary>
    protected abstract TFactory CreateFactory(string connectionString);

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        _kafkaEnvVarPrevio = Environment.GetEnvironmentVariable(KafkaBootstrapEnvVar);
        foreach (string envVar in ConnectionStringEnvVars)
        {
            _envVarsPrevios[envVar] = Environment.GetEnvironmentVariable(envVar);
        }

        try
        {
            foreach (string envVar in ConnectionStringEnvVars)
            {
                Environment.SetEnvironmentVariable(envVar, ConnectionString);
            }

            // Espaço em vez de string.Empty: em runtimes < .NET 9, string.Empty apaga
            // a variável. O host não configura transporte Kafka em teste, mas
            // neutralizamos por garantia (o routing Kafka só liga com SR + bootstrap).
            Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, " ");

            _factory = CreateFactory(ConnectionString);

            // Força o boot (build + StartAsync): migrations criam os schemas e o
            // Wolverine provisiona o outbox.
            _ = _factory.Services;
        }
        catch
        {
            RestaurarEnvVars();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        RestaurarEnvVars();

        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    private void RestaurarEnvVars()
    {
        foreach ((string envVar, string? valorPrevio) in _envVarsPrevios)
        {
            Environment.SetEnvironmentVariable(envVar, valorPrevio);
        }

        Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, _kafkaEnvVarPrevio);
    }
}

/// <summary>
/// Fixture base padrão das suítes de endpoint dos módulos: provisiona Postgres
/// efêmero e sobe a API UniPlus via <see cref="MonolitoApiFactory"/>. Suítes que
/// precisam de um composition root especializado derivam diretamente de
/// <see cref="MonolitoPostgresFixtureBase{TFactory}"/>.
/// </summary>
public class MonolitoPostgresFixture : MonolitoPostgresFixtureBase<MonolitoApiFactory>
{
    protected override MonolitoApiFactory CreateFactory(string connectionString) =>
        new(connectionString, wolverineEnabled: true);
}
