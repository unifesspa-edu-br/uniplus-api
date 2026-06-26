namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using Testcontainers.PostgreSql;

/// <summary>
/// Fixture de coleção xUnit que provisiona um Postgres efêmero (Testcontainers)
/// com o banco único <c>uniplus</c> e sobe o <see cref="MonolitoHostApiFactory"/>
/// — o composition root do monólito modular com Wolverine habilitado.
/// </summary>
/// <remarks>
/// <para>As 5 connection strings (<c>UniPlusDb</c> + uma por módulo) são injetadas
/// via env var (duplo underscore) <em>e</em> via <c>ConfigureAppConfiguration</c>
/// na factory, apontando todas para o MESMO container. Cada módulo aplica seu
/// schema (<c>HasDefaultSchema</c>) sobre o banco compartilhado; o Wolverine usa o
/// schema <c>wolverine</c>. As migrations on startup criam os 4 schemas de módulo
/// no boot.</para>
///
/// <para>Segue o padrão de <c>OrganizacaoEndpointFixture</c>.
/// <c>DisableParallelization=true</c> em <see cref="MonolitoHostCollection"/>
/// protege as env vars process-wide contra interleaving com outras coleções.</para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class MonolitoHostFixture : IAsyncLifetime
{
    private static readonly string[] ConnectionStringEnvVars =
    [
        "ConnectionStrings__UniPlusDb",
        "ConnectionStrings__ConfiguracaoDb",
        "ConnectionStrings__OrganizacaoDb",
        "ConnectionStrings__SelecaoDb",
        "ConnectionStrings__IngressoDb",
    ];

    private const string KafkaBootstrapEnvVar = "Kafka__BootstrapServers";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private readonly Dictionary<string, string?> _envVarsPrevios = [];
    private string? _kafkaEnvVarPrevio;
    private MonolitoHostApiFactory? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public MonolitoHostApiFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "Factory ainda não inicializada. InitializeAsync deve rodar antes do primeiro teste.");

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
            // a variável, fazendo o appsettings voltar a ser consultado. O host não
            // configura transporte Kafka, mas neutralizamos por garantia.
            Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, " ");

            _factory = new MonolitoHostApiFactory(ConnectionString);

            // Força o boot do host (build + StartAsync): as migrations on startup
            // dos 4 módulos criam os schemas e o Wolverine provisiona o outbox.
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
