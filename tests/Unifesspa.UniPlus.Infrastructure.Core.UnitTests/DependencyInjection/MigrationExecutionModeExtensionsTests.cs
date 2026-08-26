namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.DependencyInjection;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Infrastructure.Core.DependencyInjection;

/// <summary>
/// O modo de migration decide quem aplica o schema: o pod no boot, o Job de deploy, ou ninguém
/// (ADR-0127). O que estes testes protegem é a consequência operacional de cada escolha — em
/// especial que a ausência da chave não muda o comportamento de nenhum ambiente existente.
/// </summary>
public sealed class MigrationExecutionModeExtensionsTests
{
    private static IConfiguration Config(string? modo) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                modo is null
                    ? []
                    : new Dictionary<string, string?>
                    {
                        [MigrationExecutionModeExtensions.ChaveDeConfiguracao] = modo,
                    })
            .Build();

    private static ServiceCollection ComMigrationsRegistradas()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContext<ContextoDeTeste>(o => o.UseInMemoryDatabase("modo-migration"));
        services.AddDbContextMigrationsOnStartup<ContextoDeTeste>();
        return services;
    }

    private static int HostedServicesDeMigration(IServiceCollection services) =>
        services.Count(d => d.ServiceType == typeof(IHostedService));

    [Fact(DisplayName = "CA-01: sem a chave, o modo é OnStartup — nenhum ambiente muda por omissão")]
    public void LerModo_SemChave_ResolveParaOnStartup() =>
        Config(null).LerModoDeMigration().Should().Be(MigrationExecutionMode.OnStartup);

    [Theory(DisplayName = "Os três modos são lidos da configuração, sem diferenciar maiúsculas")]
    [InlineData("OnStartup", MigrationExecutionMode.OnStartup)]
    [InlineData("ApplyAndExit", MigrationExecutionMode.ApplyAndExit)]
    [InlineData("Skip", MigrationExecutionMode.Skip)]
    [InlineData("applyandexit", MigrationExecutionMode.ApplyAndExit)]
    [InlineData("  Skip  ", MigrationExecutionMode.Skip)]
    public void LerModo_ValorConhecido_Resolve(string valor, MigrationExecutionMode esperado) =>
        Config(valor).LerModoDeMigration().Should().Be(esperado);

    [Theory(DisplayName = "CA-05: valor fora do domínio é recusado no boot, listando os aceitos")]
    [InlineData("Nenhum")]
    [InlineData("true")]
    [InlineData("apply_and_exit")]
    public void LerModo_ValorDesconhecido_Lanca(string valor)
    {
        Action act = () => Config(valor).LerModoDeMigration();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ApplyAndExit*",
                "cair em default silencioso faria o pod aplicar migration que o Job já aplicaria");
    }

    [Fact(DisplayName = "OnStartup preserva os hosted services de migration registrados")]
    public void ConfigurarModo_OnStartup_PreservaRegistro()
    {
        ServiceCollection services = ComMigrationsRegistradas();
        int antes = HostedServicesDeMigration(services);

        services.ConfigurarModoDeMigration(MigrationExecutionMode.OnStartup);

        HostedServicesDeMigration(services).Should().Be(antes).And.BeGreaterThan(0);
    }

    [Fact(DisplayName = "CA-04: Skip remove os hosted services do pipeline de boot")]
    public void ConfigurarModo_Skip_RemoveRegistro()
    {
        ServiceCollection services = ComMigrationsRegistradas();
        HostedServicesDeMigration(services).Should().BeGreaterThan(0);

        services.ConfigurarModoDeMigration(MigrationExecutionMode.Skip);

        HostedServicesDeMigration(services).Should().Be(0);
    }

    [Fact(DisplayName = "ApplyAndExit PRESERVA os registros — são eles que o modo executa")]
    public void ConfigurarModo_ApplyAndExit_PreservaRegistro()
    {
        ServiceCollection services = ComMigrationsRegistradas();
        int antes = HostedServicesDeMigration(services);

        services.ConfigurarModoDeMigration(MigrationExecutionMode.ApplyAndExit);

        HostedServicesDeMigration(services).Should().Be(antes).And.BeGreaterThan(0,
            "remover aqui faria o processo encontrar coleção vazia, não aplicar nada e ainda "
            + "encerrar com sucesso — liberando o rollout contra o schema antigo");
    }

    [Fact(DisplayName = "Caminho real: configurar ApplyAndExit e aplicar em seguida executa as migrations registradas")]
    public async Task ConfigurarEAplicar_ApplyAndExit_ChegaAExecutarAsMigrations()
    {
        // Reproduz a sequência do composition root — configurar o modo, construir, aplicar —
        // que é onde o defeito aparecia: testar as duas peças em separado não o revelava.
        using IHost host = new HostBuilder()
            .ConfigureServices(s =>
            {
                s.AddLogging();
                s.AddDbContext<ContextoDeTeste>(o => o.UseInMemoryDatabase("caminho-real"));
                s.AddDbContextMigrationsOnStartup<ContextoDeTeste>();
                s.ConfigurarModoDeMigration(MigrationExecutionMode.ApplyAndExit);
            })
            .Build();

        int codigo = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);

        codigo.Should().Be(1,
            "1 é 'a migration falhou', e distingue-se de 2 ('nenhum contexto registrado') "
            + "justamente para provar que o serviço continuou registrado e chegou a executar. "
            + "Se ConfigurarModo removesse os registros, a saída seria 2");
    }

    [Fact(DisplayName = "ApplyAndExit sem contexto registrado encerra sem sucesso, em vez de aprovar o rollout")]
    public async Task AplicarEEncerrar_SemContextos_RetornaNaoZero()
    {
        using IHost host = new HostBuilder()
            .ConfigureServices(s => s.AddLogging())
            .Build();

        int codigo = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);

        codigo.Should().Be(2,
            "composição sem contexto algum é erro de montagem do Job, não schema em dia — e "
            + "encerrar com sucesso aqui liberaria o rollout sem ter aplicado nada");
    }

    [Fact(DisplayName = "ConfigurarModo não remove hosted services alheios à migration")]
    public void ConfigurarModo_NaoRemoveOutrosHostedServices()
    {
        ServiceCollection services = ComMigrationsRegistradas();
        services.AddHostedService<HostedServiceAlheio>();

        services.ConfigurarModoDeMigration(MigrationExecutionMode.Skip);

        services.Should().ContainSingle(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact(DisplayName = "CA-03: migration que falha devolve 1, para o Job abortar o rollout")]
    public async Task AplicarEEncerrar_Falha_RetornaNaoZero()
    {
        using IHost host = new HostBuilder()
            .ConfigureServices(s =>
            {
                s.AddLogging();
                s.AddDbContext<ContextoDeTeste>(o => o.UseInMemoryDatabase("falha-migration"));
                s.AddDbContextMigrationsOnStartup<ContextoDeTeste>();
            })
            .Build();

        int codigo = await host.AplicarMigrationsEEncerrarAsync(CancellationToken.None);

        codigo.Should().NotBe(0,
            "o provider em memória não suporta MigrateAsync — a falha precisa virar saída não-zero");
    }

    private sealed class ContextoDeTeste : DbContext
    {
        public ContextoDeTeste(DbContextOptions<ContextoDeTeste> options)
            : base(options)
        {
        }
    }

    private sealed class HostedServiceAlheio : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
