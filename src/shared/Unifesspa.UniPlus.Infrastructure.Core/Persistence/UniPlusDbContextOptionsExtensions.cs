namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

using Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

/// <summary>
/// Helpers de configuração de <see cref="DbContext"/> consumidos pelo contexto único dos módulos do monólito e pelo
/// Portal. Centraliza invariantes da camada de persistência declaradas no ADR-0054 — naming convention global
/// (snake_case automático), interceptors transversais (soft delete + audit) e leitura lazy da connection string.
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
        string connectionStringName,
        string? schema = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);

        string connectionString = ConnectionStringObrigatoria(serviceProvider, connectionStringName);

        options.UseNpgsql(connectionString, npgsqlOptions => ConfigurarMigrations<TContext>(npgsqlOptions, schema));
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
    public static DbContextOptions<TContext> BuildDesignTimeOptions<TContext>(
        string? schema = null)
        where TContext : DbContext
    {
        return new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(
                "Host=design-time-stub;Database=design_time_stub;Username=stub;Password=stub",
                npgsqlOptions => ConfigurarMigrations<TContext>(npgsqlOptions, schema))
            .UseSnakeCaseNamingConvention()
            .Options;
    }

    /// <summary>
    /// Acrescenta os interceptadores de cada módulo, na ordem dos <paramref name="modulos"/>. Chamar depois de
    /// <see cref="UseUniPlusNpgsqlConventions{TContext}"/> mantém os interceptadores compartilhados à frente: um
    /// interceptador de módulo vê o que o soft delete e a auditoria já fizeram.
    /// </summary>
    public static DbContextOptionsBuilder AdicionarInterceptadoresDosModulos(
        this DbContextOptionsBuilder options,
        IServiceProvider serviceProvider,
        IEnumerable<IContribuicaoDoModulo> modulos)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(modulos);

        foreach (IContribuicaoDoModulo modulo in modulos)
        {
            modulo.AdicionarInterceptadores(options, serviceProvider);
        }

        return options;
    }

    private static string ConnectionStringObrigatoria(IServiceProvider serviceProvider, string connectionStringName)
    {
        string? connectionString = serviceProvider
            .GetRequiredService<IConfiguration>()
            .GetConnectionString(connectionStringName);

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException(
                $"ConnectionStrings:{connectionStringName} não configurada — defina via appsettings ou env var "
                + $"`ConnectionStrings__{connectionStringName}`. Valores vazios/whitespace também são rejeitados.")
            : connectionString;
    }

    /// <summary>
    /// Fixa o assembly das migrations e o nome da tabela de histórico, que runtime e design-time precisam ver igual:
    /// sem o nome fixado, o EFCore.NamingConventions pode renomear a tabela para <c>__ef_migrations_history</c>
    /// (efcore/EFCore.NamingConventions#108), criando drift quando outro pod, sem a convention, já criou a de nome
    /// PascalCase. As colunas continuam a cargo da convention; este pin estabiliza só o nome da tabela. O
    /// <paramref name="schema"/> abriga a tabela de histórico: o monólito passa o esquema da plataforma, e quem não passa
    /// fica com o esquema padrão do banco.
    /// </summary>
    private static void ConfigurarMigrations<TContext>(NpgsqlDbContextOptionsBuilder npgsqlOptions, string? schema)
        where TContext : DbContext
    {
        npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
        npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schema);
    }

    /// <summary>
    /// Registra os interceptors transversais consumidos pelo
    /// <see cref="UseUniPlusNpgsqlConventions{TContext}"/> — Scoped por
    /// ciclo de request, pois ambos dependem de <c>IUserContext</c> scoped
    /// (HttpUserContext) para preencher <c>DeletedBy</c> / <c>CreatedBy</c> /
    /// <c>UpdatedBy</c>. Singleton aqui causaria captive dependency.
    /// </summary>
    /// <remarks>
    /// Os interceptors dependem do <see cref="TimeProvider"/> para <c>CreatedAt</c>/<c>UpdatedAt</c>/<c>DeletedAt</c>;
    /// o relógio é o registrado pelo host: esta extensão não registra relógio. O <c>TryAdd</c> mantém um só descritor de
    /// cada interceptor quando mais de um registro de persistência roda no mesmo container.
    /// </remarks>
    public static IServiceCollection AddUniPlusEfInterceptors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SoftDeleteInterceptor>();
        services.TryAddScoped<AuditableInterceptor>();

        return services;
    }
}
