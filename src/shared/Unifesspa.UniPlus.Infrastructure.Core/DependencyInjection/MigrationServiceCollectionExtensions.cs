namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using System.Reflection;

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
    /// provisionado antes de o EF rastrear as migrations), cada migration precisa ser explicitamente idempotente nos seus <c>Up()</c>
    /// (ex.: <c>CREATE TABLE IF NOT EXISTS</c>) para evitar erro 42P07 — o EF não emite guards
    /// nativos. Coordenação entre réplicas concorrentes startando simultâneo é coberta pelo
    /// lock interno do provider Npgsql sobre <c>__EFMigrationsHistory</c>, não por garantia geral
    /// de idempotência deste método.
    /// </summary>
    /// <typeparam name="TContext">DbContext cujas migrations são aplicadas (ex.: <c>MonolitoDbContext</c>, <c>PortalDbContext</c>).</typeparam>
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

            await AplicarMigrationsPendentesAsync(context, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Aplica as migrations pendentes do <paramref name="context"/>, registrando em log o que estava pendente.
    /// </summary>
    internal static async Task AplicarMigrationsPendentesAsync(
        DbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        string contextName = context.GetType().Name;
        string[] pendentes = [.. await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)];
        if (pendentes.Length == 0)
        {
            LogNoPendingMigrations(logger, contextName);
            return;
        }

        LogApplyingMigrations(logger, pendentes.Length, contextName, pendentes);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        LogMigrationsApplied(logger, contextName);
    }

    /// <summary>
    /// Registra um <see cref="IHostedService"/> que aplica as migrations EF Core do
    /// <typeparamref name="TContext"/> no <c>StartAsync</c> do host. Substitui o pattern de
    /// chamar <see cref="ApplyMigrationsAsync{TContext}"/> diretamente em <c>Program.cs</c> —
    /// como hosted service, o registro pode ser removido por test factories que sobem o
    /// pipeline sem Postgres real (alinhado com a filtragem do <c>WolverineRuntime</c> em
    /// <c>ApiFactoryBase</c>).
    /// </summary>
    /// <typeparam name="TContext">DbContext cujas migrations são aplicadas (ex.: <c>MonolitoDbContext</c>, <c>PortalDbContext</c>).</typeparam>
    /// <param name="services">A coleção de serviços do host.</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    public static IServiceCollection AddDbContextMigrationsOnStartup<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        return AdicionarMigrations<TContext>(services);
    }

    /// <summary>
    /// Igual a <see cref="AddDbContextMigrationsOnStartup{TContext}(IServiceCollection)"/>, com uma etapa que roda
    /// depois das migrations, no mesmo lugar em que elas rodam: na subida padrão e no Job <c>ApplyAndExit</c>, nunca no
    /// modo <c>Skip</c>. A etapa roda também quando não há migration pendente; a falha dela é a falha da migration.
    /// </summary>
    /// <typeparam name="TContext">DbContext do módulo.</typeparam>
    /// <param name="services">A coleção de serviços do host.</param>
    /// <param name="etapaPosterior">A etapa, que recebe o provider raiz e cria o próprio escopo.</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    public static IServiceCollection AddDbContextMigrationsOnStartup<TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, Task> etapaPosterior)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(etapaPosterior);

        services.AddSingleton(new EtapaPosteriorDaMigration<TContext>(etapaPosterior));
        return AdicionarMigrations<TContext>(services);
    }

    /// <summary>
    /// Igual a <see cref="AddDbContextMigrationsOnStartup{TContext}(IServiceCollection, Func{IServiceProvider, CancellationToken, Task})"/>,
    /// com os scripts SQL que cada assembly embute em <c>Persistence/Scripts</c>. Sob uma trava por banco, os de <c>Pre</c>
    /// rodam antes das migrations; os demais, depois delas e antes da etapa posterior. Cada script roda numa transação
    /// própria, em ordem ordinal pelo nome do recurso dentro do assembly, e os assemblies na ordem recebida. Rodam na
    /// subida padrão e no Job <c>ApplyAndExit</c>, nunca no modo <c>Skip</c>, e também sem migration pendente.
    /// </summary>
    /// <typeparam name="TContext">DbContext cujo banco os scripts preparam.</typeparam>
    /// <param name="services">A coleção de serviços do host.</param>
    /// <param name="assembliesDosScripts">Os assemblies que embutem os scripts, na ordem de aplicação.</param>
    /// <param name="etapaPosterior">A etapa, que recebe o provider raiz e cria o próprio escopo.</param>
    /// <returns>A própria <paramref name="services"/> para encadeamento fluente.</returns>
    public static IServiceCollection AddDbContextMigrationsOnStartup<TContext>(
        this IServiceCollection services,
        IReadOnlyList<Assembly> assembliesDosScripts,
        Func<IServiceProvider, CancellationToken, Task> etapaPosterior)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembliesDosScripts);
        ArgumentNullException.ThrowIfNull(etapaPosterior);

        services.AddSingleton(new AssembliesDosScriptsDoContexto<TContext>(assembliesDosScripts));
        services.AddSingleton(new EtapaPosteriorDaMigration<TContext>(etapaPosterior));
        return AdicionarMigrations<TContext>(services);
    }

    /// <summary>
    /// Sobe o banco do contexto: com scripts registrados, pela subida sob trava; sem eles, só as migrations. A etapa
    /// posterior registrada roda por último.
    /// </summary>
    internal static async Task MigrarEExecutarEtapaPosteriorAsync<TContext>(
        this IServiceProvider services,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        EtapaPosteriorDaMigration<TContext>? etapa = services.GetService<EtapaPosteriorDaMigration<TContext>>();
        AssembliesDosScriptsDoContexto<TContext>? scripts = services.GetService<AssembliesDosScriptsDoContexto<TContext>>();

        if (scripts is null)
        {
            await services.ApplyMigrationsAsync<TContext>(cancellationToken).ConfigureAwait(false);
            await ExecutarEtapaAsync(etapa, services, cancellationToken).ConfigureAwait(false);
            return;
        }

        await PrepararOBancoAsync(services, scripts, etapa, cancellationToken).ConfigureAwait(false);
    }

    private static async Task PrepararOBancoAsync<TContext>(
        IServiceProvider services,
        AssembliesDosScriptsDoContexto<TContext> scripts,
        EtapaPosteriorDaMigration<TContext>? etapa,
        CancellationToken cancellationToken)
        where TContext : DbContext
    {
        AsyncServiceScope escopo = services.CreateAsyncScope();
        await using (escopo.ConfigureAwait(false))
        {
            await PreparacaoDoBanco
                .ExecutarAsync(
                    escopo.ServiceProvider.GetRequiredService<TContext>(),
                    escopo.ServiceProvider.GetRequiredService<ILogger<TContext>>(),
                    scripts.Assemblies,
                    token => ExecutarEtapaAsync(etapa, services, token),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static Task ExecutarEtapaAsync<TContext>(
        EtapaPosteriorDaMigration<TContext>? etapa,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where TContext : DbContext =>
        etapa is null ? Task.CompletedTask : etapa.Executar(services, cancellationToken);

    private static IServiceCollection AdicionarMigrations<TContext>(IServiceCollection services)
        where TContext : DbContext
    {

        // Singleton com TryAddEnumerable previne dupla-registração se Program.cs chamar duas
        // vezes com o mesmo TContext (defesa em profundidade — Program.cs deve chamar uma só).
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, MigrationHostedService<TContext>>());

        // Registro paralelo, para o modo ApplyAndExit aplicar as migrations sem materializar a
        // coleção de hosted services — que traria junto warmup de criptografia, mensageria e
        // schema registry, e faria o Job falhar por configuração alheia ao schema.
        ObterRegistroDeContextos(services)
            .Registrar(typeof(TContext), (provider, token) => provider.MigrarEExecutarEtapaPosteriorAsync<TContext>(token));

        return services;
    }

    /// <summary>
    /// O registro de contextos do <paramref name="services"/>, criando-o na primeira chamada.
    /// </summary>
    internal static MigrationContextRegistry ObterRegistroDeContextos(IServiceCollection services)
    {
        ServiceDescriptor? existente = services.FirstOrDefault(
            d => d.ServiceType == typeof(MigrationContextRegistry));

        if (existente?.ImplementationInstance is MigrationContextRegistry registro)
        {
            return registro;
        }

        MigrationContextRegistry novo = new();
        services.AddSingleton(novo);
        return novo;
    }

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Nenhuma migration EF Core pendente para {Context}.")]
    private static partial void LogNoPendingMigrations(ILogger logger, string context);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Aplicando {Count} migration(s) EF Core para {Context}: {Migrations}")]
    private static partial void LogApplyingMigrations(ILogger logger, int count, string context, IReadOnlyList<string> migrations);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Migrations EF Core aplicadas com sucesso para {Context}.")]
    private static partial void LogMigrationsApplied(ILogger logger, string context);
}
