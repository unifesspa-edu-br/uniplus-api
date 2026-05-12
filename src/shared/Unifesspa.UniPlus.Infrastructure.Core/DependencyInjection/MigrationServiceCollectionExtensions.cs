namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Helpers de aplicação de migrations EF Core no startup do host. Pareados com a configuração
/// <c>AutoBuildMessageStorageOnStartup</c> do Wolverine em
/// <see cref="Messaging.WolverineOutboxConfiguration"/> para destravar startups em ambientes
/// onde o schema dos módulos ainda não foi provisionado por orquestração externa
/// (lab/standalone, primeira subida de pod com banco vazio).
/// </summary>
public static partial class MigrationServiceCollectionExtensions
{
    /// <summary>
    /// Aplica todas as migrations EF Core pendentes do <typeparamref name="TContext"/>. Em banco
    /// já migrado (com a migration registrada em <c>__EFMigrationsHistory</c>), retorna sem efeito.
    /// Para bancos com schema pré-existente que ainda não foi rastreado pelo EF (caso de standalone
    /// pré-#416), cada migration precisa ser explicitamente idempotente nos seus <c>Up()</c>
    /// (ex.: <c>CREATE TABLE IF NOT EXISTS</c>) para evitar erro 42P07 — o EF não emite guards
    /// nativos. Coordenação entre réplicas concorrentes startando simultâneo é coberta pelo
    /// lock interno do provider Npgsql sobre <c>__EFMigrationsHistory</c>, não por garantia geral
    /// de idempotência deste método.
    /// </summary>
    /// <typeparam name="TContext">DbContext do módulo (PortalDbContext, SelecaoDbContext, IngressoDbContext).</typeparam>
    /// <param name="services">
    /// <see cref="IServiceProvider"/> raiz (em <c>Program.cs</c>: <c>app.Services</c>).
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento — opcional.</param>
    /// <remarks>
    /// O método cria um scope dedicado para resolver o DbContext (que é registrado scoped em
    /// <c>AddDbContext</c>); chamar do scope raiz lançaria
    /// <see cref="InvalidOperationException"/>. Logging via <see cref="ILogger{TCategoryName}"/>
    /// reporta migrations pendentes ANTES e APÓS aplicação — visível no Loki/console em prod.
    /// </remarks>
    public static async Task ApplyMigrationsAsync<TContext>(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
            ILogger<TContext> logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

            IEnumerable<string> pending = await context.Database
                .GetPendingMigrationsAsync(cancellationToken)
                .ConfigureAwait(false);
            string[] pendingArray = [.. pending];
            string contextName = typeof(TContext).Name;

            if (pendingArray.Length == 0)
            {
                LogNoPendingMigrations(logger, contextName);
                return;
            }

#pragma warning disable CA1873 // Avaliação cara protegida por IsEnabled — caminho de startup, baixíssima frequência.
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogApplyingMigrations(logger, pendingArray.Length, contextName, string.Join(", ", pendingArray));
            }
#pragma warning restore CA1873

            await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            LogMigrationsApplied(logger, contextName);
        }
    }

    /// <summary>
    /// Registra um <see cref="IHostedService"/> que aplica as migrations EF Core do
    /// <typeparamref name="TContext"/> no <c>StartAsync</c> do host. Substitui o pattern de
    /// chamar <see cref="ApplyMigrationsAsync{TContext}"/> diretamente em <c>Program.cs</c> —
    /// como hosted service, o registro pode ser removido por test factories que sobem o
    /// pipeline sem Postgres real (alinhado com a filtragem do <c>WolverineRuntime</c> em
    /// <c>ApiFactoryBase</c>).
    /// </summary>
    /// <typeparam name="TContext">DbContext do módulo (PortalDbContext, SelecaoDbContext, IngressoDbContext).</typeparam>
    /// <param name="services">A coleção de serviços do host.</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    public static IServiceCollection AddDbContextMigrationsOnStartup<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        // Singleton com TryAddEnumerable previne dupla-registração se Program.cs chamar duas
        // vezes com o mesmo TContext (defesa em profundidade — Program.cs deve chamar uma só).
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, MigrationHostedService<TContext>>());

        return services;
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Nenhuma migration EF Core pendente para {Context}.")]
    private static partial void LogNoPendingMigrations(ILogger logger, string context);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Aplicando {Count} migration(s) EF Core para {Context}: {Migrations}")]
    private static partial void LogApplyingMigrations(ILogger logger, int count, string context, string migrations);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Migrations EF Core aplicadas com sucesso para {Context}.")]
    private static partial void LogMigrationsApplied(ILogger logger, string context);
}
