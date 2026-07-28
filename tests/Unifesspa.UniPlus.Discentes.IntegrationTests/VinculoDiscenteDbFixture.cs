namespace Unifesspa.UniPlus.Discentes.IntegrationTests;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Discentes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o schema do
/// <see cref="DiscentesDbContext"/> aplicado via <c>MigrateAsync</c>, e expõe uma
/// factory de DbContext com os MESMOS interceptors da produção, mais um
/// <see cref="LocalAesEncryptionService"/> real (chave determinística de teste) para
/// provar o caminho de cifragem real (ADR-0121), sem depender de Vault.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class VinculoDiscenteDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_discentes_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>
    /// Mesmo provedor local (AES-GCM) que roda em dev/CI, resolvido via o mesmo
    /// <c>AddUniPlusEncryption</c> público usado em produção — chave de 32 bytes fixa,
    /// só para determinismo do teste (nunca reutilizar em ambiente real). A
    /// implementação concreta (<c>LocalAesEncryptionService</c>) é <c>internal</c> ao
    /// assembly de Infrastructure.Core, por isso é resolvida via DI, não instanciada
    /// diretamente.
    /// </summary>
    public IUniPlusEncryptionService Encryption { get; } = BuildLocalEncryptionService();

    private static IUniPlusEncryptionService BuildLocalEncryptionService()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddUniPlusEncryption(configure: options =>
        {
            options.Provider = "local";
            options.LocalKey = Convert.ToBase64String(new byte[32]);
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IUniPlusEncryptionService>();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using DiscentesDbContext context = CreateDbContext();
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    public DiscentesDbContext CreateDbContext()
    {
        DbContextOptions<DiscentesDbContext> options =
            new DbContextOptionsBuilder<DiscentesDbContext>()
                .UseNpgsql(ConnectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    new SoftDeleteInterceptor(TimeProvider.System),
                    new AuditableInterceptor(TimeProvider.System))
                .Options;

        return new DiscentesDbContext(options);
    }
}
