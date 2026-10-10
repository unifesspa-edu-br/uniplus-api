namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o schema do
/// <see cref="SelecaoDbContext"/> aplicado via <c>MigrateAsync</c>, e expõe uma factory de
/// DbContext com os interceptors da produção, mais um provedor local real (AES-GCM + HMAC) para
/// provar o caminho de cifragem e índice cego (ADR-0121) da inscrição do candidato (issue #1858),
/// sem depender de Vault — mesmo desenho de <c>VinculoDiscenteDbFixture</c> (Discentes).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class InscricaoDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_inscricao_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public IUniPlusEncryptionService Encryption { get; }

    public IUniPlusBlindIndexService BlindIndex { get; }

    public InscricaoDbFixture()
    {
        ServiceCollection services = new();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddUniPlusEncryption(configure: options =>
        {
            options.Provider = "local";
            options.LocalKey = Convert.ToBase64String(new byte[32]);
        });

        ServiceProvider provider = services.BuildServiceProvider();
        Encryption = provider.GetRequiredService<IUniPlusEncryptionService>();
        BlindIndex = provider.GetRequiredService<IUniPlusBlindIndexService>();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using SelecaoDbContext context = CreateDbContext();
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    public SelecaoDbContext CreateDbContext()
    {
        DbContextOptions<SelecaoDbContext> options = new DbContextOptionsBuilder<SelecaoDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System),
                new AuditableInterceptor(TimeProvider.System))
            .Options;

        return new SelecaoDbContext(options);
    }
}
