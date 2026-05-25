using JasperFx;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia.Handlers;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Marten;
using Wolverine.Postgresql;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;

/// <summary>
/// Host da prova de coabitação: no MESMO processo/cluster, o outbox EF Core
/// (<c>PersistMessagesWithPostgresql</c>, store <b>'main'</b> — o plano operacional,
/// preserva ADR-0004) convive com o Event Store do Marten registrado como store
/// <b>'ancillary'</b> (<c>AddMartenStore&lt;IEditalEsStore&gt;</c>). Resolve o
/// requisito do Wolverine de "exatamente um store main". O <paramref name="modo"/>
/// exercita cluster (<c>Balanced</c>) no teste multi-nó.
/// </summary>
public static class ConfiguracaoCoexistencia
{
    public const string SchemaMensageria = "wolverine_cx";   // store 'main' (EF Core)
    public const string SchemaEventosEs = "edital_es_cx";    // event store ancillary (Marten)

    public static IHostBuilder CriarHost(string connectionString, DurabilityMode modo = DurabilityMode.Solo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                // Nome distinto: namespaceia o código gerado do Wolverine, evitando
                // colisão de tipos gerados com o host ES-only no mesmo processo.
                opts.ServiceName = "uniplus-spike-cx";

                // Módulo CRUD: EF Core como store 'main' (plano operacional de mensageria).
                opts.Services.AddDbContext<CrudDbContext>(o => o.UseNpgsql(connectionString));
                opts.PersistMessagesWithPostgresql(connectionString, SchemaMensageria);
                opts.UseEntityFrameworkCoreTransactions();

                // Módulo event-sourced: Marten como store 'ancillary' (event store).
                opts.Services.AddMartenStore<IEditalEsStore>(marten =>
                {
                    marten.Connection(connectionString);
                    marten.DatabaseSchemaName = SchemaEventosEs;
                    marten.AutoCreateSchemaObjects = AutoCreate.All;
                })
                // Sem lambda: o store ancillary COMPARTILHA o envelope storage do
                // store 'main' — é o "message store operacional compartilhado" da proposta.
                .IntegrateWithWolverine();

                opts.Services.AddSingleton(TimeProvider.System);
                opts.Services.AddSingleton<ColetorCoexistencia>();

                // Discovery escopado: só os handlers de coabitação (evita arrastar os
                // handlers ES do store primário, que não existem neste host).
                opts.Discovery.DisableConventionalDiscovery()
                    .IncludeType(typeof(CriarRegistroCrudHandler))
                    .IncludeType(typeof(RegistroCrudCriadoHandler));

                opts.Policies.AutoApplyTransactions();
                opts.Policies.UseDurableLocalQueues();

                opts.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
                opts.Durability.Mode = modo;

                // Cluster: intervalos curtos para eleição/failover rápidos em teste
                // (em produção os defaults na casa de segundos/minutos são adequados).
                if (modo == DurabilityMode.Balanced)
                {
                    opts.Durability.HealthCheckPollingTime = TimeSpan.FromSeconds(1);
                    opts.Durability.FirstHealthCheckExecution = TimeSpan.FromSeconds(1);
                    opts.Durability.NodeReassignmentPollingTime = TimeSpan.FromSeconds(1);
                    opts.Durability.FirstNodeReassignmentExecution = TimeSpan.FromSeconds(1);
                    opts.Durability.CheckAssignmentPeriod = TimeSpan.FromSeconds(1);
                    opts.Durability.StaleNodeTimeout = TimeSpan.FromSeconds(3);
                }
            });
    }
}
