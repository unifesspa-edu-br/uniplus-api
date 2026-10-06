using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

using Testcontainers.PostgreSql;

using Wolverine;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Spike882.Agendamento;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public string Conexao => _postgres.GetConnectionString();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
}

public sealed class SpikeAgendamentoTests(PostgresFixture postgres, ITestOutputHelper saida) : IClassFixture<PostgresFixture>
{
    // ---- 1. Réplica única -------------------------------------------------------------

    [Fact]
    public async Task Replica_unica_executa_uma_vez_com_duas_replicas()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost a = Hosts.Criar(conexao, agendamentoDuravel: true);
        using IHost b = Hosts.Criar(conexao, agendamentoDuravel: true);
        await a.StartAsync();
        await b.StartAsync();

        await a.Services.GetRequiredService<IMessageBus>()
            .ScheduleAsync(new ProbeAgendado("r1"), DateTimeOffset.UtcNow.AddSeconds(6));
        await Task.Delay(TimeSpan.FromSeconds(12));
        await a.StopAsync();
        await b.StopAsync();

        Imprimir("duas réplicas, agendamento único");
        Assert.Equal(1, Registro.Contar("probe:r1"));
    }

    [Fact]
    public async Task Failover_B_executa_quando_A_para_depois_de_agendar()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost a = Hosts.Criar(conexao, agendamentoDuravel: true);
        using IHost b = Hosts.Criar(conexao, agendamentoDuravel: true);
        await a.StartAsync();
        await b.StartAsync();

        await a.Services.GetRequiredService<IMessageBus>()
            .ScheduleAsync(new ProbeAgendado("f1"), DateTimeOffset.UtcNow.AddSeconds(6));
        await a.StopAsync(); // A sai antes do vencimento; B continua no ar
        await EsperarAsync(() => Registro.Contar("probe:f1") > 0, TimeSpan.FromSeconds(25));
        await b.StopAsync();

        Imprimir("failover: A parou antes do vencimento");
        Assert.Equal(1, Registro.Contar("probe:f1"));
    }

    // ---- 2. Queda real do processo ---------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queda_do_processo_so_preserva_agendamento_duravel(bool duravel)
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add(CaminhoDoHost());
        psi.ArgumentList.Add(conexao);
        psi.ArgumentList.Add(duravel ? "duravel" : "sem-duravel");
        psi.ArgumentList.Add("agendar");

        using Process filho = Process.Start(psi)!;
        string linha = await filho.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal("AGENDADO", linha);
        filho.Kill(entireProcessTree: true); // queda abrupta, antes do vencimento (~10 s)
        filho.WaitForExit();

        using IHost novo = Hosts.Criar(conexao, duravel);
        await novo.StartAsync();
        await EsperarAsync(() => Registro.Contar("probe:morto") > 0, TimeSpan.FromSeconds(25));
        await novo.StopAsync();

        Imprimir($"queda real, duravel={duravel}");
        Assert.Equal(duravel, Registro.Contar("probe:morto") > 0);
    }

    // ---- 3. Retry no gatilho ---------------------------------------------------------

    [Fact]
    public async Task Retry_no_gatilho_recupera_falha_transitoria()
    {
        Registro.Reiniciar();
        Registro.FalhasRestantesNoGatilho = 1;
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true, comRetry: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new Gatilho(1, UsarClaim: true));
        await EsperarAsync(() => Registro.Contar("gatilho:2") > 0, TimeSpan.FromSeconds(25));
        await host.StopAsync();

        Imprimir("retry no gatilho com falha transitória");
        Assert.True(Registro.Contar("gatilho:2") >= 1);
    }

    // ---- 4. Reagendar e falhar: dead letters -----------------------------------------

    [Fact]
    public async Task ReScheduleCurrent_seguido_de_falha_conta_dead_letters()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new Reagenda("b", FalharAposReagendar: true));
        await Task.Delay(TimeSpan.FromSeconds(15));
        await host.StopAsync();

        int deadLetters = await ContarAsync(conexao, "SELECT count(*) FROM wolverine.wolverine_dead_letters");
        Imprimir($"reagenda+falha: execuções={Registro.ContarPrefixo("reagenda:b:")}, dead_letters={deadLetters}");
        Assert.True(Registro.ContarPrefixo("reagenda:b:") >= 2);
    }

    // ---- 5. Provisionamento Skip com role sem DDL -----------------------------------

    [Fact]
    public async Task Provisionamento_Skip_com_role_sem_DDL_agenda_normalmente()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        // Como a produção: o schema é criado antes, por um processo com privilégio.
        using (IHost preparo = Hosts.Criar(conexao, agendamentoDuravel: true))
        {
            await preparo.StartAsync();
            await preparo.StopAsync();
        }

        string role = "spike_nodo_" + Guid.NewGuid().ToString("N")[..6];
        string banco = new NpgsqlConnectionStringBuilder(conexao).Database!;
        await ExecutarAsync(conexao,
            $"CREATE ROLE {role} LOGIN PASSWORD 'spike';" +
            $"GRANT CONNECT ON DATABASE {banco} TO {role};" +
            $"GRANT USAGE ON SCHEMA wolverine TO {role};" +
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA wolverine TO {role};" +
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA wolverine TO {role};" +
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON spike_claim TO {role};");

        string conexaoRole = new NpgsqlConnectionStringBuilder(conexao) { Username = role, Password = "spike" }.ConnectionString;

        using (IHost host = Hosts.Criar(conexaoRole, agendamentoDuravel: true, somenteLeituraDeSchema: true))
        {
            await host.StartAsync();
            await host.Services.GetRequiredService<IMessageBus>()
                .ScheduleAsync(new ProbeAgendado("skip"), DateTimeOffset.UtcNow.AddSeconds(6));
            await host.StopAsync();
        }

        using (IHost host = Hosts.Criar(conexaoRole, agendamentoDuravel: true, somenteLeituraDeSchema: true))
        {
            await host.StartAsync();
            await EsperarAsync(() => Registro.Contar("probe:skip") > 0, TimeSpan.FromSeconds(25));
            await host.StopAsync();
        }

        // A role realmente não pode criar objetos.
        PostgresException? ddl = null;
        try
        {
            await ExecutarAsync(conexaoRole, "CREATE TABLE teste_ddl(x int)");
        }
        catch (PostgresException erro)
        {
            ddl = erro;
        }

        Imprimir($"Skip + role sem DDL (DDL recusado: {ddl?.SqlState ?? "NÃO RECUSADO"})");
        Assert.Equal("42501", ddl?.SqlState); // insufficient_privilege
        Assert.Equal(1, Registro.Contar("probe:skip"));
    }

    // ---- Cadeia (já validada antes) --------------------------------------------------

    [Fact]
    public async Task Cadeia_sem_claim_com_duplicata_mantem_duas_cadeias()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true);
        await host.StartAsync();
        IMessageBus bus = host.Services.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(new Gatilho(1, UsarClaim: false));
        await bus.PublishAsync(new Gatilho(1, UsarClaim: false));
        await EsperarAsync(() => Registro.Contar("gatilho:3") > 0, TimeSpan.FromSeconds(30));
        await host.StopAsync();

        Imprimir("cadeia sem claim");
        Assert.Equal(2, Registro.Contar("sync:1"));
        Assert.True(Registro.Contar("gatilho:3") >= 2);
    }

    [Fact]
    public async Task Cadeia_com_claim_unico_por_tick_nao_duplica()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true);
        await host.StartAsync();
        IMessageBus bus = host.Services.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(new Gatilho(1, UsarClaim: true));
        await bus.PublishAsync(new Gatilho(1, UsarClaim: true));
        await EsperarAsync(() => Registro.Contar("gatilho:3") > 0, TimeSpan.FromSeconds(30));
        await host.StopAsync();

        Imprimir("cadeia com claim");
        Assert.Equal(1, Registro.Contar("sync:1"));
        Assert.Equal(1, Registro.Contar("noop:1"));
        Assert.Equal(1, Registro.Contar("gatilho:3"));
    }

    [Fact]
    public async Task Cadeia_para_quando_o_gatilho_falha_antes_de_agendar()
    {
        Registro.Reiniciar();
        Registro.FalharGatilhoAntesDeAgendar = true;
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new Gatilho(1, UsarClaim: true));
        await Task.Delay(TimeSpan.FromSeconds(12));
        await host.StopAsync();

        Imprimir("gatilho falha antes de agendar");
        Assert.Equal(1, Registro.Contar("gatilho:1"));
        Assert.Equal(0, Registro.Contar("gatilho:2"));
    }

    // ---- Auxiliares -------------------------------------------------------------------

    private static string CaminhoDoHost([CallerFilePath] string arquivo = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(arquivo)!, "..", "Spike882.Host", "bin", "Debug", "net10.0", "Spike882.Host.dll"));

    private async Task<string> NovoBancoAsync()
    {
        string nome = "spike_" + Guid.NewGuid().ToString("N")[..12];

        await ExecutarAsync(postgres.Conexao, $"CREATE DATABASE {nome}");

        string conexao = new NpgsqlConnectionStringBuilder(postgres.Conexao) { Database = nome }.ConnectionString;
        await ExecutarAsync(conexao, "CREATE TABLE spike_claim(tick int PRIMARY KEY)");
        return conexao;
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new NpgsqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<int> ContarAsync(string conexao, string sql)
    {
        await using var c = new NpgsqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, c);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    private static async Task EsperarAsync(Func<bool> condicao, TimeSpan limite)
    {
        DateTime fim = DateTime.UtcNow + limite;
        while (!condicao() && DateTime.UtcNow < fim)
        {
            await Task.Delay(200);
        }
    }

    private void Imprimir(string titulo)
    {
        saida.WriteLine($"== {titulo}");
        foreach (string evento in Registro.Todos())
        {
            saida.WriteLine(evento);
        }
    }
}
