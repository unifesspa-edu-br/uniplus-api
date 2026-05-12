namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the fixture type to be public.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources are released by IAsyncLifetime.DisposeAsync, which xUnit invokes deterministically for fixtures.")]
public sealed class CascadingFixture : IAsyncLifetime
{
    // Overrides são injetados via env vars (formato `<Section>__<Key>`) porque, em
    // hosting minimal (WebApplication.CreateBuilder), overrides via
    // ConfigureAppConfiguration de WebApplicationFactory.ConfigureWebHost não chegam
    // a WebApplicationBuilder.Configuration. Env vars sempre entram, e como esta
    // coleção tem DisableParallelization=true, não há risco de interleaving com
    // outras coleções no mesmo processo.
    //
    // Kafka é forçado a empty para desligar o transporte Kafka durante os testes —
    // appsettings.Development.json define `localhost:9092` por padrão para dev local,
    // o que faria Wolverine ficar em retry indefinido contra um broker inexistente
    // no host de teste.
    private const string ConnectionStringEnvVar = "ConnectionStrings__SelecaoDb";
    private const string KafkaBootstrapEnvVar = "Kafka__BootstrapServers";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_outbox_cascading")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    private string? _connectionStringEnvVarPrevio;
    private string? _kafkaEnvVarPrevio;
    private CascadingApiFactory? _factory;

    public string ConnectionString => _postgres.GetConnectionString();

    public CascadingApiFactory Factory =>
        _factory ?? throw new InvalidOperationException(
            "Factory ainda não inicializada. InitializeAsync deve rodar antes do primeiro teste.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        // Cria o schema do domínio Selecao no Postgres efêmero ANTES do host
        // Wolverine inicializar — sem isso há disputa de timing com o
        // PostgresqlTransport e o handler reporta `relation "editais" does not exist`.
        // O schema do Wolverine (`wolverine.*`) agora é provisionado no startup pelo
        // próprio framework via `AutoBuildMessageStorageOnStartup = CreateOrUpdate`
        // (issue #344) — antes ficava off-by-default e era criado lazy no primeiro
        // despacho, o que era origem de timing flakiness em testes outbox.
        //
        // Usar MigrateAsync (e não EnsureCreatedAsync) garante coabitação com o
        // MigrationHostedService que o host produtivo carrega no startup
        // (uniplus-api#416): popular __EFMigrationsHistory aqui faz o
        // MigrationHostedService achar a migration já aplicada e ser no-op,
        // evitando colisão 42P07 (relation already exists).
        //
        // Esta duplicidade fixture↔MigrationHostedService é tática e está
        // documentada em ADR-0039 §"Atualizações posteriores". Follow-up
        // planejado em uniplus-api#419: reordenar IHostedService no
        // Program.cs (MigrationHostedService antes de UseWolverineOutboxCascading)
        // + fitness test + remoção desta linha, restaurando 1 fonte de verdade.
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using SelecaoDbContext db = new(options);
        await db.Database.MigrateAsync().ConfigureAwait(false);

        // Captura todos os valores prévios ANTES de mutar o environment —
        // garantia de restore-em-falha dos dois sets atômicos. Sem isto,
        // uma exceção entre os dois SetEnvironmentVariable abaixo deixaria
        // a primeira variável setada para o resto do test run (issue #195).
        _connectionStringEnvVarPrevio = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        _kafkaEnvVarPrevio = Environment.GetEnvironmentVariable(KafkaBootstrapEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, ConnectionString);

            // Whitespace (espaço) em vez de string.Empty: em runtimes anteriores a
            // .NET 9, Environment.SetEnvironmentVariable(name, string.Empty) apaga
            // a variável (em vez de definir como vazia), o que faria o appsettings
            // voltar a ser consultado e o Wolverine tentar conectar em
            // localhost:9092. O helper produtivo desliga Kafka via IsNullOrWhiteSpace,
            // então um espaço cobre os dois cenários sem regressão cross-runtime.
            Environment.SetEnvironmentVariable(KafkaBootstrapEnvVar, " ");

            _factory = new CascadingApiFactory(ConnectionString);
        }
        catch
        {
            // Restore inline antes de relançar — DisposeAsync é chamado por xUnit
            // mesmo após InitializeAsync falhar, mas só se a fixture chegou a
            // ser construída. Restaurar aqui torna a fixture resiliente mesmo
            // em cenários onde xUnit pula o dispose por outro motivo.
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
