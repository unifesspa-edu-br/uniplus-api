using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;
using Wolverine;
using Wolverine.Runtime;
using Xunit;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Tests;

/// <summary>
/// Gate G2 host-level — passo 3 (cluster): duas réplicas (dois <see cref="IHost"/> em
/// <c>DurabilityMode.Balanced</c> sobre o mesmo PostgreSQL) elegem um líder; ao parar o
/// líder, a réplica sobrevivente assume a liderança, o nó morto é ejetado e o
/// processamento continua. A eleição é coordenada pelo banco — dois IHosts são dois
/// nós reais, não simulação.
/// </summary>
public sealed class MultiNoLeaderElectionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("uniplus_spike_cluster")
        .WithUsername("uniplus_spike")
        .WithPassword("uniplus_spike")
        .Build();

    private IHost _noA = null!;
    private IHost _noB = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        string conn = _postgres.GetConnectionString();

        _noA = ConfiguracaoCoexistencia.CriarHost(conn, DurabilityMode.Balanced).Build();
        await _noA.StartAsync();

        // Cria as tabelas do módulo CRUD uma vez (schema já existe via Marten/Wolverine).
        using (IServiceScope escopo = _noA.Services.CreateScope())
        {
            CrudDbContext db = escopo.ServiceProvider.GetRequiredService<CrudDbContext>();
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        _noB = ConfiguracaoCoexistencia.CriarHost(conn, DurabilityMode.Balanced).Build();
        await _noB.StartAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (IHost? no in new[] { _noA, _noB })
        {
            if (no is not null)
            {
                try { await no.StopAsync(); no.Dispose(); }
                catch (ObjectDisposedException) { /* já parado no teste de failover */ }
            }
        }

        await _postgres.DisposeAsync();
    }

    [Fact(DisplayName = "G2 cluster: dois nós elegem líder; failover transfere liderança e ejeta o nó morto")]
    public async Task Cluster_elege_lider_e_faz_failover()
    {
        Guid idA = IdDoNo(_noA);
        Guid idB = IdDoNo(_noB);
        idA.Should().NotBe(idB, "cada réplica é um nó distinto");

        // 1. Ambos os nós registram
        bool doisNos = await TestHelpers.EsperarAsync(
            () => ContarNosAsync().GetAwaiter().GetResult() == 2, TimeSpan.FromSeconds(15));
        doisNos.Should().BeTrue("as duas réplicas devem registrar em wolverine_nodes");

        // 2. Exatamente um líder eleito
        Guid? lider = await EsperarLiderAsync(TimeSpan.FromSeconds(20));
        lider.Should().NotBeNull("a eleição deve produzir um líder");
        new[] { idA, idB }.Should().Contain(lider!.Value, "o líder é uma das duas réplicas");

        // 3. Failover: para o nó LÍDER
        IHost hostLider = lider == idA ? _noA : _noB;
        IHost hostSobrevivente = lider == idA ? _noB : _noA;
        Guid idSobrevivente = lider == idA ? idB : idA;
        await hostLider.StopAsync();
        hostLider.Dispose();

        // 4. Nó morto é ejetado e a liderança passa ao sobrevivente
        bool ejetado = await TestHelpers.EsperarAsync(
            () => ContarNosAsync().GetAwaiter().GetResult() == 1, TimeSpan.FromSeconds(30));
        ejetado.Should().BeTrue("o nó parado deve ser ejetado de wolverine_nodes");

        bool novaLideranca = await TestHelpers.EsperarAsync(
            () => LiderAtualAsync().GetAwaiter().GetResult() == idSobrevivente, TimeSpan.FromSeconds(30));
        novaLideranca.Should().BeTrue("a réplica sobrevivente deve assumir a liderança");

        // 5. Continuidade: o sobrevivente segue processando
        Guid id = Guid.CreateVersion7();
        IMessageBus bus = hostSobrevivente.Services.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new CriarRegistroCrud(id, "pós-failover"));

        ColetorCoexistencia coletor = hostSobrevivente.Services.GetRequiredService<ColetorCoexistencia>();
        bool entregue = await TestHelpers.EsperarAsync(() => coletor.Contem(id), TimeSpan.FromSeconds(15));
        entregue.Should().BeTrue("o cluster segue entregando mensagens após o failover");
    }

    private static Guid IdDoNo(IHost host)
    {
        IWolverineRuntime runtime = host.Services.GetRequiredService<IWolverineRuntime>();
        return runtime.Options.UniqueNodeId;
    }

    private async Task<Guid?> EsperarLiderAsync(TimeSpan timeout)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < limite)
        {
            Guid? lider = await LiderAtualAsync();
            if (lider is not null)
            {
                return lider;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return await LiderAtualAsync();
    }

    private async Task<Guid?> LiderAtualAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand cmd = conexao.CreateCommand();
        cmd.CommandText = $"""
            SELECT node_id FROM {ConfiguracaoCoexistencia.SchemaMensageria}.wolverine_node_assignments
            WHERE id ILIKE '%leader%' LIMIT 1;
            """;
        object? raw = await cmd.ExecuteScalarAsync();
        return raw is null or DBNull ? null : (Guid)raw;
    }

    private async Task<int> ContarNosAsync()
    {
        await using NpgsqlConnection conexao = new(_postgres.GetConnectionString());
        await conexao.OpenAsync();
        await using NpgsqlCommand cmd = conexao.CreateCommand();
        cmd.CommandText = $"SELECT count(*) FROM {ConfiguracaoCoexistencia.SchemaMensageria}.wolverine_nodes;";
        object? raw = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(raw, System.Globalization.CultureInfo.InvariantCulture);
    }
}
