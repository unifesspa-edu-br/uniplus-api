namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

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
    /// <param name="configurarNpgsql">
    /// Hook opcional para compor o bloco <c>UseNpgsql</c> com extensões do
    /// provider Npgsql (ex.: <c>o =&gt; o.UseNetTopologySuite()</c> no módulo Geo,
    /// ADR-0091). Executado <em>depois</em> de <c>MigrationsAssembly</c> e
    /// <c>MigrationsHistoryTable</c>, dentro do mesmo callback. Default
    /// <c>null</c> — não-invasivo: módulos que não passam o hook mantêm o
    /// comportamento atual inalterado.
    /// </param>
    public static DbContextOptionsBuilder UseUniPlusNpgsqlConventions<TContext>(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider,
        string connectionStringName,
        Action<NpgsqlDbContextOptionsBuilder>? configurarNpgsql = null,
        string? schema = null)
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
            //
            // Monólito modular: quando `schema` é informado, a tabela de
            // histórico vive no schema do módulo (banco único, schema-por-módulo).
            // O default schema do modelo é fixado no `OnModelCreating` de cada
            // DbContext via `HasDefaultSchema(Schema)` — aqui só alinhamos a
            // tabela de histórico ao mesmo schema.
            if (schema is null)
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
            }
            else
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            }

            // Composição opcional do provider (ex.: UseNetTopologySuite no Geo).
            configurarNpgsql?.Invoke(npgsqlOptions);
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
    /// Constrói <see cref="DbContextOptions{TContext}"/> para uso de
    /// <c>IDesignTimeDbContextFactory&lt;T&gt;</c> consumidos pelo
    /// <c>dotnet ef</c> CLI. Compartilha com o runtime as duas decisões
    /// sensíveis à geração de migrations:
    /// <list type="bullet">
    ///   <item><see cref="UseSnakeCaseNamingConvention"/> garante que o SQL
    ///   emitido pela migration nasça em snake_case (ADR-0054);</item>
    ///   <item><c>MigrationsHistoryTable("__EFMigrationsHistory")</c> evita
    ///   split-brain entre o nome da tabela usado em design-time (ex.:
    ///   <c>dotnet ef migrations script</c>) e o usado em runtime — caso
    ///   contrário, o EF poderia inserir/consultar histórias em duas tabelas
    ///   distintas.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// A connection string é sintética porque migrations EF Core não conectam
    /// ao banco durante <c>migrations add</c>. <c>database update</c> rodado
    /// localmente sobrescreve via <c>--connection</c>.
    /// </remarks>
    /// <param name="configurarNpgsql">
    /// Hook opcional idêntico ao de <see cref="UseUniPlusNpgsqlConventions{TContext}"/>
    /// — garante paridade runtime↔design-time. O módulo Geo passa
    /// <c>o =&gt; o.UseNetTopologySuite()</c> para que <c>dotnet ef migrations</c>
    /// gere o mapeamento <c>geography(Point,4326)</c>. Default <c>null</c>.
    /// </param>
    public static DbContextOptions<TContext> BuildDesignTimeOptions<TContext>(
        Action<NpgsqlDbContextOptionsBuilder>? configurarNpgsql = null,
        string? schema = null)
        where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                "Host=design-time-stub;Database=design_time_stub;Username=stub;Password=stub",
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                    if (schema is null)
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                    }
                    else
                    {
                        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schema);
                    }

                    configurarNpgsql?.Invoke(npgsqlOptions);
                })
            .UseSnakeCaseNamingConvention()
            .Options;
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

        // Relógio canônico (ADR de convenção de TimeProvider): os interceptors
        // dependem dele para CreatedAt/UpdatedAt/DeletedAt. TryAdd preserva
        // overrides de teste (TimeProvider fixo) e é idempotente com os demais
        // pontos que já registram TimeProvider.System (paginação, idempotência).
        services.TryAddSingleton(TimeProvider.System);

        // TryAdd: cada módulo chama AddUniPlusEfInterceptors no seu
        // Add{Modulo}Infrastructure. No co-hosting (monólito modular) isso roda
        // N vezes no mesmo container; sem o guard, o container acumularia N
        // descritores idênticos de cada interceptor. Os interceptors não têm
        // estado por módulo (resolvem IUserContext/TimeProvider scoped), então
        // um único registro serve a todos os DbContexts.
        services.TryAddScoped<SoftDeleteInterceptor>();
        services.TryAddScoped<AuditableInterceptor>();

        return services;
    }
}
