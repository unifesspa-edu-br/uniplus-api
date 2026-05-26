using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Microsoft.EntityFrameworkCore;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Host;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
using Wolverine.Postgresql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string conexao = Environment.GetEnvironmentVariable("SPIKE_DB")
    ?? throw new InvalidOperationException("Variável de ambiente SPIKE_DB não configurada.");

builder.Host.UseWolverine(opts =>
{
    // Topologia co-hospedada: EF Core como store 'main' (operacional) + Marten como
    // store 'ancillary' (event store, com projeção EditalEs inline para FetchForWriting).
    opts.Services.AddDbContext<CrudDbContext>(o => o.UseNpgsql(conexao));
    opts.PersistMessagesWithPostgresql(conexao, ConfiguracaoCoexistencia.SchemaMensageria);
    opts.UseEntityFrameworkCoreTransactions();

    opts.Services.AddMartenStore<IEditalEsStore>(marten =>
    {
        marten.Connection(conexao);
        marten.DatabaseSchemaName = ConfiguracaoCoexistencia.SchemaEventosEs;
        marten.Projections.Snapshot<EditalEs>(SnapshotLifecycle.Inline);
        marten.AutoCreateSchemaObjects = AutoCreate.All;
    })
    .IntegrateWithWolverine();

    opts.Discovery.IncludeAssembly(typeof(HostMarker).Assembly);

    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();
    opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;

    // Cluster: réplica em modo Balanced (eleição de líder coordenada pelo banco).
    opts.Durability.Mode = DurabilityMode.Balanced;

    // Concorrência otimista por stream sob escala: o writer perdedor é retentado.
    opts.OnException<EventStreamUnexpectedMaxEventIdException>()
        .RetryWithCooldown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
});

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/es/{id:guid}/abrir", async (Guid id, IMessageBus bus) =>
{
    await bus.InvokeAsync(new AbrirEditalEs(id, "001/2026", "Edital de escala"));
    return Results.Accepted();
});

app.MapPost("/es/{id:guid}/retificar", async (Guid id, IMessageBus bus) =>
{
    await bus.InvokeAsync(new RetificarEditalEs(id, "retificação concorrente"));
    return Results.Accepted();
});

app.MapGet("/es/{id:guid}/retificacoes", async (Guid id, IEditalEsStore store) =>
{
    await using IQuerySession sessao = store.QuerySession();
    EditalEs? view = await sessao.LoadAsync<EditalEs>(id);
    return Results.Ok(view?.QuantidadeRetificacoes ?? 0);
});

await app.RunAsync();
