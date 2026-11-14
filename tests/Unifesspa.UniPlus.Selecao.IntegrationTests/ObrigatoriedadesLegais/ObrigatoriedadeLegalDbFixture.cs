namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ObrigatoriedadesLegais;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Interceptors;
using Unifesspa.UniPlus.Monolito.Persistencia;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Fixture xUnit que provisiona um Postgres efêmero (Testcontainers) com o
/// schema do <see cref="UniPlusDbContext"/> subido por <c>SubirOBancoAsync</c>,
/// e expõe uma factory de <see cref="UniPlusDbContext"/> com os MESMOS
/// interceptors da produção (Soft Delete + Auditable + Histórico de
/// <c>ObrigatoriedadeLegal</c>).
/// </summary>
/// <remarks>
/// <para>
/// Story #460 exige validação ponta-a-ponta de:
/// <list type="bullet">
///   <item>Persistência de <c>ObrigatoriedadeLegal</c> + materialização do
///   payload jsonb canônico do predicado.</item>
///   <item>Inserção atômica em <c>obrigatoriedade_legal_historico</c> via
///   <see cref="ObrigatoriedadeLegalHistoricoInterceptor"/>.</item>
///   <item>UNIQUE parcial sobre <c>hash</c> (apenas para linhas vigentes).</item>
///   <item>btree_gist + EXCLUDE constraint sobre janelas temporais da
///   junction <c>obrigatoriedade_legal_areas_de_interesse</c>.</item>
/// </list>
/// </para>
/// <para>
/// Esta fixture é deliberadamente independente da <c>CascadingFixture</c>
/// (que re-registra o DbContext apenas com Soft Delete + Auditable para
/// outbox messaging). Aqui o foco é o ciclo de vida do save de regras,
/// sem subir o Wolverine/Kafka/HTTP pipeline.
/// </para>
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit IAsyncLifetime + IClassFixture<T> exigem tipo público.")]
[SuppressMessage(
    "Reliability",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposable resources released by IAsyncLifetime.DisposeAsync — xUnit invoca deterministicamente.")]
public sealed class ObrigatoriedadeLegalDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_obrigatoriedade_legal_tests")
        .WithUsername("uniplus_test")
        .WithPassword("uniplus_test")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        await using UniPlusDbContext context = CreateDbContext(userId: null);
        await context.SubirOBancoAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Constrói um <see cref="UniPlusDbContext"/> com os 3 interceptors da
    /// produção. Quando <paramref name="userId"/> é informado, simula um
    /// <c>IUserContext</c> autenticado para o teste — caso contrário, os
    /// interceptors caem para o fallback <c>"system"</c>.
    /// </summary>
    public UniPlusDbContext CreateDbContext(string? userId)
    {
        StubUserContext? userContext = userId is null ? null : new StubUserContext(userId);

        DbContextOptions<MonolitoDbContext> options = new DbContextOptionsBuilder<MonolitoDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new SoftDeleteInterceptor(TimeProvider.System, userContext),
                new AuditableInterceptor(TimeProvider.System, userContext),
                new ObrigatoriedadeLegalHistoricoInterceptor(TimeProvider.System, userContext))
            .Options;

        return new MonolitoDbContext(options);
    }
}
