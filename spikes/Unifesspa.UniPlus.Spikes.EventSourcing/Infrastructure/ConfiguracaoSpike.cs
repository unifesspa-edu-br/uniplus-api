using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Pii;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure;

/// <summary>
/// Monta o host do spike: Marten (Event Store) + projeção inline + integração
/// transacional com o outbox do Wolverine. Reaproveitável por testes e por um
/// eventual host de demonstração.
/// </summary>
public static class ConfiguracaoSpike
{
    /// <summary>Schema PostgreSQL isolado para eventos e tabelas do Marten/Wolverine.</summary>
    public const string SchemaEventos = "edital_es";

    public static IHostBuilder CriarHost(string connectionString, DurabilityMode modo = DurabilityMode.Solo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                // Nome distinto: namespaceia o código gerado do Wolverine, evitando
                // colisão de tipos gerados com o host de coabitação no mesmo processo.
                opts.ServiceName = "uniplus-spike-es";

                opts.Services.AddMarten(marten =>
                {
                    marten.Connection(connectionString);
                    marten.DatabaseSchemaName = SchemaEventos;

                    // Projeção single-stream síncrona: read model consistente com o append.
                    marten.Projections.Snapshot<EditalEs>(SnapshotLifecycle.Inline);

                    // Spike/test: deixa o Marten criar o schema. Em produção isso é
                    // responsabilidade do deploy (ADR-0039), não auto-create em runtime.
                    marten.AutoCreateSchemaObjects = AutoCreate.All;
                })
                .IntegrateWithWolverine()
                .UseLightweightSessions();

                // Relógio injetável (ADR-0068): determinismo em teste, sem DateTime.UtcNow.
                opts.Services.AddSingleton(TimeProvider.System);

                // Coletor de eventos de integração entregues (observabilidade de teste).
                opts.Services.AddSingleton<ColetorIntegracao>();

                // Protetor de PII: singleton sem estado (gere sessões próprias via
                // IDocumentStore), exposto por duas portas. As chaves vivem num
                // unit-of-work separado — modela um cofre de chaves desacoplado.
                opts.Services.AddSingleton(sp => new ProtetorPiiAesGcm(sp.GetRequiredService<IDocumentStore>()));
                opts.Services.AddSingleton<IProtetorPii>(sp => sp.GetRequiredService<ProtetorPiiAesGcm>());
                opts.Services.AddSingleton<IServicoEsquecimento>(sp => sp.GetRequiredService<ProtetorPiiAesGcm>());

                // Discovery escopado APENAS aos handlers ES deste host (ADR-0043). Não
                // usa IncludeAssembly para não arrastar os handlers do módulo de
                // coabitação (que dependem de CrudDbContext / store ancillary) e evitar
                // contaminação da geração de código entre hosts no mesmo processo de teste.
                opts.Discovery.DisableConventionalDiscovery()
                    .IncludeType(typeof(AbrirEditalHandler))
                    .IncludeType(typeof(PublicarEditalHandler))
                    .IncludeType(typeof(RetificarEditalHandler))
                    .IncludeType(typeof(FalharAposAnexarHandler))
                    .IncludeType(typeof(EditalPublicadoIntegradoHandler));

                // Atomicidade write+evento: a middleware aplica SaveChanges e instala
                // os envelopes do outbox na mesma transação do append.
                opts.Policies.AutoApplyTransactions();

                // Filas locais duráveis: o evento de integração cascateado passa pelo
                // outbox (persistido na mesma transação), não é processado inline.
                opts.Policies.UseDurableLocalQueues();

                // Solo (default) para os testes single-node; Balanced para o teste de
                // escala horizontal (≥2 réplicas com eleição de líder).
                opts.Durability.Mode = modo;

                // Concorrência otimista de stream sob escala: quando duas réplicas
                // gravam no mesmo agregado, o perdedor é retentado com estado fresco
                // (FetchForWriting recarrega a versão) — converge sem lost update.
                opts.OnException<EventStreamUnexpectedMaxEventIdException>()
                    .RetryWithCooldown(
                        TimeSpan.FromMilliseconds(50),
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(250),
                        TimeSpan.FromMilliseconds(500),
                        TimeSpan.FromSeconds(1));
            });
    }
}
