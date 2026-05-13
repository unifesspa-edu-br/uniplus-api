namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Interceptors;

/// <summary>
/// Helpers de configuração de <see cref="DbContext"/> consumidos pelos 3
/// módulos (Selecao, Ingresso, Portal). Centraliza invariantes da camada de
/// persistência declaradas no ADR-0054 — naming convention global
/// (snake_case automático), interceptors transversais (soft delete + audit)
/// e leitura lazy da connection string. Substitui código duplicado nos
/// <c>Add{Module}Infrastructure</c>.
/// </summary>
public static class UniPlusDbContextOptionsExtensions
{
    /// <summary>
    /// Configura o <paramref name="options"/> com o stack canônico Uni+:
    /// <list type="bullet">
    ///   <item><c>UseNpgsql</c> com a connection string lida do
    ///   <see cref="IConfiguration"/> + <c>MigrationsAssembly</c> apontando para
    ///   o assembly do <typeparamref name="TContext"/>;</item>
    ///   <item><see cref="SoftDeleteInterceptor"/> + <see cref="AuditableInterceptor"/>
    ///   adicionados via <c>AddInterceptors</c> (resolvidos do
    ///   <paramref name="serviceProvider"/> Scoped).</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para><b>EnableRetryOnFailure deliberadamente NÃO configurado.</b> O
    /// <c>NpgsqlRetryingExecutionStrategy</c> é incompatível com as
    /// user-initiated transactions abertas pela política
    /// <c>AutoApplyTransactions</c> + <c>EnrollDbContextInTransaction</c> do
    /// Wolverine outbox (ver <c>WolverineOutboxConfiguration</c> em
    /// Infrastructure.Core/Messaging). Resiliência a falhas transientes fica
    /// a cargo das policies de retry do Wolverine no nível do envelope.</para>
    ///
    /// <para><b>Connection string lida lazy</b> dentro do factory do
    /// <c>AddDbContext</c> — alinha-se com o padrão lazy do
    /// <c>UseWolverineOutboxCascading</c> (issue #204). Test hosts que
    /// sobrescrevem <c>ConnectionStrings:{name}</c> via env var ou
    /// <c>InMemoryCollection</c> ganham o override automaticamente, sem
    /// re-registrar o DbContext.</para>
    /// </remarks>
    public static DbContextOptionsBuilder UseUniPlusNpgsqlConventions<TContext>(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider,
        string connectionStringName)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
        string? connectionString = configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{connectionStringName} não configurada — defina via appsettings ou env var "
                + $"`ConnectionStrings__{connectionStringName}`. Valores vazios/whitespace também são rejeitados.");
        }

        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);

            // EFCore.NamingConventions aplica snake_case também na tabela
            // system `__EFMigrationsHistory` (issue efcore/EFCore.NamingConventions#108).
            // Sem pin do nome, a convention pode renomear a tabela para
            // `__ef_migrations_history`, criando drift quando outro pod
            // (sem a convention) já criou com o nome PascalCase default
            // do EF Core. Pin garante que todos os pods do rollout enxerguem
            // a mesma tabela. As COLUNAS continuam sendo processadas pela
            // convention — em rollouts mistos com pods sem convention, o
            // drift de colunas ainda exige drop+recreate; este pin estabiliza
            // apenas o nome da tabela.
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
        });

        // snake_case automático em tabelas, colunas, índices e FKs (ADR-0054).
        // Alinhado com as migrations regeneradas via `dotnet ef migrations add`
        // usando `--startup-project = .API`.
        options.UseSnakeCaseNamingConvention();

        options.AddInterceptors(
            serviceProvider.GetRequiredService<SoftDeleteInterceptor>(),
            serviceProvider.GetRequiredService<AuditableInterceptor>());

        return options;
    }

    /// <summary>
    /// Registra os interceptors transversais consumidos pelo
    /// <see cref="UseUniPlusNpgsqlConventions{TContext}"/> — Scoped por
    /// ciclo de request, pois ambos dependem de <c>IUserContext</c> scoped
    /// (HttpUserContext) para preencher <c>DeletedBy</c> / <c>CreatedBy</c> /
    /// <c>UpdatedBy</c>. Singleton aqui causaria captive dependency.
    /// </summary>
    public static IServiceCollection AddUniPlusEfInterceptors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableInterceptor>();

        return services;
    }
}
