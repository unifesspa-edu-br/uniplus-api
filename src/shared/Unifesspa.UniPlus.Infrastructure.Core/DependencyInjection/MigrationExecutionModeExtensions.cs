namespace Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

using JasperFx.Resources;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Leitura e aplicação do <see cref="MigrationExecutionMode"/> no composition root.
/// </summary>
/// <remarks>
/// Os três modos operam sobre os mesmos <see cref="IHostedService"/> de migration que os
/// módulos já registram (<see cref="MigrationServiceCollectionExtensions.AddDbContextMigrationsOnStartup{TContext}"/>):
/// <c>Skip</c> os remove antes do <c>Build()</c>, e <c>ApplyAndExit</c> executa apenas eles.
/// Nenhum módulo precisa saber em que modo o processo está.
/// </remarks>
public static partial class MigrationExecutionModeExtensions
{
    /// <summary>Chave de configuração que declara o papel do processo.</summary>
    public const string ChaveDeConfiguracao = "UniPlus:Migrations:Mode";

    /// <summary>
    /// Lê o modo declarado. A ausência da chave resolve para
    /// <see cref="MigrationExecutionMode.OnStartup"/>, preservando o comportamento de quem
    /// não declara nada.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Se a chave traz um valor fora do domínio. Recusar no boot é deliberado: cair em default
    /// silencioso faria um pod destinado a <c>Skip</c> aplicar migration por conta própria, que
    /// é exatamente o que a separação existe para impedir.
    /// </exception>
    public static MigrationExecutionMode LerModoDeMigration(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? valor = configuration[ChaveDeConfiguracao];
        if (string.IsNullOrWhiteSpace(valor))
        {
            return MigrationExecutionMode.OnStartup;
        }

        string informado = valor.Trim();
        foreach (MigrationExecutionMode modo in Enum.GetValues<MigrationExecutionMode>()
            .Where(m => string.Equals(informado, m.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return modo;
        }

        throw new InvalidOperationException(
            $"Valor inválido em '{ChaveDeConfiguracao}': '{valor}'. "
            + $"Valores aceitos: {string.Join(", ", Enum.GetNames<MigrationExecutionMode>())}.");
    }

    /// <summary>
    /// Como o Wolverine deve tratar o schema do outbox, dado o modo declarado.
    /// </summary>
    /// <remarks>
    /// O Wolverine provisiona as próprias tabelas no start do runtime, fora do mecanismo de
    /// migrations EF. Em <see cref="MigrationExecutionMode.Skip"/> quem provisiona é o Job de
    /// deploy — deixar o pod aplicar DDL traria de volta, pela mensageria, a falha depois do
    /// rollout que a separação existe para impedir. Nos demais modos o comportamento é o de
    /// sempre.
    /// </remarks>
    public static JasperFx.AutoCreate ProvisionamentoDeSchemaPara(MigrationExecutionMode modo) =>
        modo == MigrationExecutionMode.Skip
            ? JasperFx.AutoCreate.None
            : JasperFx.AutoCreate.CreateOrUpdate;

    /// <summary>
    /// Remove os hosted services de migration quando o modo é
    /// <see cref="MigrationExecutionMode.Skip"/>. Chamar antes do <c>Build()</c>.
    /// </summary>
    /// <remarks>
    /// <b>Só <c>Skip</c> remove.</b> <c>ApplyAndExit</c> precisa desses mesmos registros — são
    /// eles que <see cref="AplicarMigrationsEEncerrarAsync"/> executa. Removê-los ali faria o
    /// processo encontrar uma coleção vazia, não aplicar migration alguma e ainda assim encerrar
    /// com sucesso, liberando o rollout contra o schema antigo: uma falha que se reporta como
    /// aprovação, que é o pior desfecho possível para este mecanismo.
    /// </remarks>
    public static IServiceCollection ConfigurarModoDeMigration(
        this IServiceCollection services,
        MigrationExecutionMode modo)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (modo != MigrationExecutionMode.Skip)
        {
            return services;
        }

        foreach (ServiceDescriptor descritor in services.Where(EhServicoDeMigration).ToList())
        {
            services.Remove(descritor);
        }

        return services;
    }

    /// <summary>
    /// Executa apenas as migrations e devolve o código de saída do processo:
    /// <c>0</c> aplicou, <c>1</c> a migration ou o provisionamento falhou, <c>2</c> não havia
    /// contexto algum registrado. Os dois erros são distintos porque pedem remédios distintos: o primeiro é
    /// schema ou dado, o segundo é o Job montado errado — e confundi-los custaria tempo no pior
    /// momento. Nenhum outro <see cref="IHostedService"/> é iniciado — construir o host
    /// resolve o container, mas não inicia serviço algum, então mensageria e pipeline HTTP não
    /// chegam a subir.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Fronteira de processo: o código de saída é o único canal pelo qual o Job "
            + "de deploy sabe se o rollout pode seguir, e qualquer falha de migration — do provider, "
            + "do banco ou do próprio EF — precisa virar saída não-zero em vez de exceção não tratada.")]
    public static async Task<int> AplicarMigrationsEEncerrarAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(MigrationExecutionModeExtensions).FullName!);

        IHostedService[] migrations =
            [.. host.Services.GetServices<IHostedService>().Where(EhInstanciaDeMigration)];

        if (migrations.Length == 0)
        {
            LogNenhumaMigrationRegistrada(logger);
            return 2;
        }

        try
        {
            // Migrations EF não são o único schema que a aplicação provisiona: o Wolverine
            // cria e atualiza as tabelas do outbox por conta própria, pelo
            // AutoBuildMessageStorageOnStartup. Sem isto, o Job aplicaria só o schema dos
            // módulos e devolveria sucesso, e o DDL do outbox continuaria acontecendo no boot
            // do pod — trazendo de volta, pela mensageria, a falha depois do rollout que esta
            // separação existe para impedir.
            //
            // SetupResources provisiona os IStatefulResource registrados — o do Wolverine
            // incluso — sem iniciar o runtime de mensageria: nenhuma fila é consumida e
            // nenhum listener sobe.
            await host.SetupResources(cancellationToken).ConfigureAwait(false);
            foreach (IHostedService migration in migrations)
            {
                await migration.StartAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception excecao)
        {
            LogFalhaAoAplicarMigrations(logger, excecao);
            return 1;
        }

        LogMigrationsConcluidas(logger, migrations.Length);
        return 0;
    }

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Information,
        Message = "Migrations aplicadas por {Contextos} contexto(s); encerrando sem servir requisições.")]
    private static partial void LogMigrationsConcluidas(ILogger logger, int contextos);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Critical,
        Message = "Nenhum contexto de migration registrado — o processo não teria o que aplicar. "
            + "Encerrando sem sucesso para que o rollout não prossiga achando que o schema foi aplicado.")]
    private static partial void LogNenhumaMigrationRegistrada(ILogger logger);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Critical,
        Message = "Falha ao aplicar migrations. O rollout não deve prosseguir.")]
    private static partial void LogFalhaAoAplicarMigrations(ILogger logger, Exception excecao);

    private static bool EhServicoDeMigration(ServiceDescriptor descritor) =>
        descritor.ServiceType == typeof(IHostedService)
        && descritor.ImplementationType is { IsGenericType: true } tipo
        && tipo.GetGenericTypeDefinition() == typeof(MigrationHostedService<>);

    private static bool EhInstanciaDeMigration(IHostedService servico) =>
        servico.GetType() is { IsGenericType: true } tipo
        && tipo.GetGenericTypeDefinition() == typeof(MigrationHostedService<>);
}
