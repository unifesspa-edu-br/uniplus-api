using System.Diagnostics;
using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;

using Wolverine;
using Xunit;
using Xunit.Abstractions;

namespace Spike882.Agendamento;

/// <summary>Cenários no caminho de produção: handler com EF, outbox transacional e reserva atômica.</summary>
public sealed class SpikeProducaoTests(PostgresFixture postgres, ITestOutputHelper saida) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Cadeia_atomica_avanca_em_ticks_sequenciais()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true, caminhoProducao: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1));
        await EsperarAsync(() => Registro.Contar("gatilho-at:3") > 0, TimeSpan.FromSeconds(30));
        await host.StopAsync();

        Imprimir("cadeia atômica, caminho de produção");
        Assert.Equal(1, Registro.Contar("sync:1"));
        Assert.Equal(1, Registro.Contar("sync:2"));
        Assert.Equal(1, Registro.Contar("sync:3"));
    }

    [Fact]
    public async Task Duplicata_atomica_nao_duplica_a_cadeia()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true, comRetry: true, caminhoProducao: true);
        await host.StartAsync();
        IMessageBus bus = host.Services.GetRequiredService<IMessageBus>();
        await bus.PublishAsync(new GatilhoAtomico(1));
        await bus.PublishAsync(new GatilhoAtomico(1));
        await EsperarAsync(() => Registro.Contar("gatilho-at:3") > 0, TimeSpan.FromSeconds(30));
        await host.StopAsync();

        Imprimir("duplicata atômica");
        Assert.Equal(1, Registro.Contar("sync:1"));
        Assert.Equal(1, Registro.Contar("sync:2"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT count(*) FROM tick_reservado WHERE \"Tick\" = 1"));
    }

    [Fact]
    public async Task Retry_atomico_reprocessa_o_mesmo_tick()
    {
        Registro.Reiniciar();
        Registro.FalhasRestantesNoGatilho = 1;
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true, comRetry: true, caminhoProducao: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1));
        await EsperarAsync(() => Registro.Contar("gatilho-at:2") > 0, TimeSpan.FromSeconds(30));
        await host.StopAsync();

        Imprimir("retry atômico");
        Assert.True(Registro.Contar("gatilho-at:2") >= 1);
        Assert.Equal(1, Registro.Contar("sync:1"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT count(*) FROM tick_reservado WHERE \"Tick\" = 1"));
    }

    [Fact]
    public async Task Duas_replicas_no_caminho_de_producao_nao_duplicam()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        using IHost a = Hosts.Criar(conexao, agendamentoDuravel: true, comRetry: true, caminhoProducao: true);
        using IHost b = Hosts.Criar(conexao, agendamentoDuravel: true, comRetry: true, caminhoProducao: true);
        await a.StartAsync();
        await b.StartAsync();

        // Mesmo tick entregue a duas réplicas ao mesmo tempo.
        await Task.WhenAll(
            a.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1)).AsTask(),
            b.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1)).AsTask());

        await EsperarAsync(() => Registro.Contar("gatilho-at:3") > 0, TimeSpan.FromSeconds(30));
        await a.StopAsync();
        await b.StopAsync();

        Imprimir("duas réplicas, mesmo tick simultâneo");
        Assert.Equal(1, Registro.Contar("sync:1"));
        Assert.Equal(1, Registro.Contar("sync:2"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT count(*) FROM tick_reservado WHERE \"Tick\" = 1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queda_do_processo_entre_disparos_preserva_a_cadeia(bool duravel)
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add(CaminhoDoHost());
        psi.ArgumentList.Add(conexao);
        psi.ArgumentList.Add(duravel ? "duravel" : "sem-duravel");
        psi.ArgumentList.Add("cadeia");

        using Process filho = Process.Start(psi)!;
        string linha = await filho.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal("TICK3", linha);
        filho.Kill(entireProcessTree: true); // tick 4 já está agendado no outbox
        filho.WaitForExit();

        using IHost novo = Hosts.Criar(conexao, agendamentoDuravel: duravel, caminhoProducao: true);
        await novo.StartAsync();
        await EsperarAsync(() => Registro.Contar("gatilho-at:4") > 0, TimeSpan.FromSeconds(30));
        await novo.StopAsync();

        Imprimir($"queda entre disparos (processo real), duravel={duravel}");
        Assert.Equal(duravel ? 1 : 0, Registro.Contar("gatilho-at:4"));
    }

    [Fact]
    public async Task Cadeia_atomica_continua_quando_a_sincronizacao_falha()
    {
        Registro.Reiniciar();
        Registro.FalharSincronizacao = true;
        string conexao = await NovoBancoAsync();

        using IHost host = Hosts.Criar(conexao, agendamentoDuravel: true, caminhoProducao: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1));
        await EsperarAsync(() => Registro.Contar("gatilho-at:2") > 0, TimeSpan.FromSeconds(30));
        await host.StopAsync();

        Imprimir("continuidade atômica com sincronização falhando");
        Assert.Equal(1, Registro.Contar("sync-inicio:1")); // a sincronização foi tentada e falhou
        Assert.True(Registro.Contar("gatilho-at:2") >= 1);
        Assert.Equal(1, await ContarAsync(conexao, "SELECT count(*) FROM tick_reservado WHERE \"Tick\" = 1"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Queda_durante_a_sincronizacao_preserva_o_proximo_disparo(bool duravel)
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add(CaminhoDoHost());
        psi.ArgumentList.Add(conexao);
        psi.ArgumentList.Add(duravel ? "duravel" : "sem-duravel");
        psi.ArgumentList.Add("cadeia-lenta");

        using Process filho = Process.Start(psi)!;
        string linha = await filho.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal("SYNC2_INICIADA", linha);
        filho.Kill(entireProcessTree: true); // morto com a sincronização do tick 2 em andamento
        filho.WaitForExit();

        using IHost novo = Hosts.Criar(conexao, agendamentoDuravel: duravel, caminhoProducao: true);
        await novo.StartAsync();
        await EsperarAsync(() => Registro.Contar("gatilho-at:3") > 0, TimeSpan.FromSeconds(30));
        await novo.StopAsync();

        await Task.Delay(TimeSpan.FromSeconds(40)); // espera a reatribuição de um nó morto, se houver
        Imprimir($"queda durante a sincronização, duravel={duravel}");
        Assert.Equal(duravel ? 1 : 0, Registro.Contar("gatilho-at:3"));
        Assert.Equal(0, Registro.Contar("sync:2")); // sem filas locais duráveis, a sincronização em andamento se perde
    }

    [Fact]
    public async Task Queda_durante_a_sincronizacao_com_filas_locais_duraveis_reexecuta_a_sincronizacao()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();

        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add(CaminhoDoHost());
        psi.ArgumentList.Add(conexao);
        psi.ArgumentList.Add("duravel");
        psi.ArgumentList.Add("cadeia-lenta");
        psi.ArgumentList.Add("filas-duraveis");

        using Process filho = Process.Start(psi)!;
        string linha = await filho.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal("SYNC2_INICIADA", linha);
        filho.Kill(entireProcessTree: true);
        filho.WaitForExit();

        using IHost novo = Hosts.Criar(conexao, agendamentoDuravel: true, caminhoProducao: true, filasLocaisDuraveis: true);
        await novo.StartAsync();
        await EsperarAsync(() => Registro.Contar("sync:2") > 0 && Registro.Contar("gatilho-at:3") > 0, TimeSpan.FromSeconds(90));
        await novo.StopAsync();

        Imprimir("queda durante a sincronização, filas locais duráveis");
        Assert.Equal(1, Registro.Contar("sync:2"));
        Assert.Equal(1, Registro.Contar("gatilho-at:3"));
    }

    [Fact]
    public async Task Role_com_grant_na_tabela_de_reserva_mantem_a_cadeia()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();
        string role = await CriarRoleAsync(conexao, comTabelaDeReserva: true);
        string conexaoRole = new NpgsqlConnectionStringBuilder(conexao) { Username = role, Password = "spike" }.ConnectionString;

        using IHost host = Hosts.Criar(conexaoRole, agendamentoDuravel: true, somenteLeituraDeSchema: true, caminhoProducao: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1));
        await EsperarAsync(() => Registro.Contar("gatilho-at:3") > 0, TimeSpan.FromSeconds(30));
        await Task.Delay(TimeSpan.FromSeconds(1)); // deixa o commit do tick 3 terminar
        await host.StopAsync();

        Imprimir("role com grant na tabela de reserva");
        Assert.Equal(1, Registro.Contar("sync:2"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT count(*) FROM tick_reservado WHERE \"Tick\" = 2"));
    }

    [Fact]
    public async Task Role_sem_grant_na_tabela_de_reserva_para_a_cadeia_e_gera_dead_letter()
    {
        Registro.Reiniciar();
        string conexao = await NovoBancoAsync();
        string role = await CriarRoleAsync(conexao, comTabelaDeReserva: false);
        string conexaoRole = new NpgsqlConnectionStringBuilder(conexao) { Username = role, Password = "spike" }.ConnectionString;

        using IHost host = Hosts.Criar(conexaoRole, agendamentoDuravel: true, somenteLeituraDeSchema: true, caminhoProducao: true);
        await host.StartAsync();
        await host.Services.GetRequiredService<IMessageBus>().PublishAsync(new GatilhoAtomico(1));
        await Task.Delay(TimeSpan.FromSeconds(12));
        await host.StopAsync();

        int deadLetters = await ContarAsync(conexao, "SELECT count(*) FROM wolverine.wolverine_dead_letters");
        Imprimir($"role sem grant na tabela de reserva (dead letters={deadLetters})");
        Assert.Equal(0, Registro.Contar("gatilho-at:1"));
        Assert.True(deadLetters >= 1);
    }

    // ---- Auxiliares -------------------------------------------------------------------

    private async Task<string> CriarRoleAsync(string conexao, bool comTabelaDeReserva)
    {
        // O schema do Wolverine é criado antes, por um processo com privilégio (como o job de deploy).
        using (IHost preparo = Hosts.Criar(conexao, agendamentoDuravel: true, caminhoProducao: true))
        {
            await preparo.StartAsync();
            await preparo.StopAsync();
        }

        string role = "spike_nodo_" + Guid.NewGuid().ToString("N")[..6];
        string banco = new NpgsqlConnectionStringBuilder(conexao).Database!;
        string sql =
            $"CREATE ROLE {role} LOGIN PASSWORD 'spike';" +
            $"GRANT CONNECT ON DATABASE {banco} TO {role};" +
            $"GRANT USAGE ON SCHEMA wolverine TO {role};" +
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA wolverine TO {role};" +
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA wolverine TO {role};";
        if (comTabelaDeReserva)
        {
            sql += $"GRANT SELECT, INSERT, UPDATE, DELETE ON tick_reservado TO {role};";
        }

        await ExecutarAsync(conexao, sql);
        return role;
    }

    private static string CaminhoDoHost([CallerFilePath] string arquivo = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(arquivo)!, "..", "Spike882.Host", "bin", "Debug", "net10.0", "Spike882.Host.dll"));

    private async Task<string> NovoBancoAsync()
    {
        string nome = "spike_" + Guid.NewGuid().ToString("N")[..12];
        await ExecutarAsync(postgres.Conexao, $"CREATE DATABASE {nome}");

        string conexao = new NpgsqlConnectionStringBuilder(postgres.Conexao) { Database = nome }.ConnectionString;
        var opcoes = new DbContextOptionsBuilder<SpikeDbContext>().UseNpgsql(conexao).Options;
        await using var banco = new SpikeDbContext(opcoes);
        await banco.Database.EnsureCreatedAsync();
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
