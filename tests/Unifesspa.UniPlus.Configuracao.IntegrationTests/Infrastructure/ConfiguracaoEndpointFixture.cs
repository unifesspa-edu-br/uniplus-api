namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Testcontainers.PostgreSql;

/// <summary>
/// Fixture de coleção xUnit que provisiona um Postgres efêmero (Testcontainers)
/// e sobe o <see cref="ConfiguracaoEndpointApiFactory"/> com Wolverine habilitado
/// — necessário para exercitar endpoints que chamam o query/command bus.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class ConfiguracaoEndpointFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__ConfiguracaoDb";
    private const string KafkaBootstrapEnvVar = "Kafka__BootstrapServers";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_configuracao_endpoint_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private string? _connectionStringEnvVarPrevio;
    private string? _kafkaEnvVarPrevio;
    private ConfiguracaoEndpointApiFactory? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public ConfiguracaoEndpointApiFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "Factory ainda não inicializada. InitializeAsync deve rodar antes do primeiro teste.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        _connectionStringEnvVarPrevio = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        _kafkaEnvVarPrevio = Environment.GetEnvironmentVariable(KafkaBootstrapEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, ConnectionString);
            // Espaço em vez de string.Empty: em runtimes < .NET 9, string.Empty apaga
            // a variável, fazendo o appsettings voltar a ser consultado.
            Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, " ");

            _factory = new ConfiguracaoEndpointApiFactory(ConnectionString);
        }
        catch
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _connectionStringEnvVarPrevio);
            Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, _kafkaEnvVarPrevio);
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _connectionStringEnvVarPrevio);
        Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, _kafkaEnvVarPrevio);

        await _postgres.DisposeAsync().ConfigureAwait(false);
    }
}
