using JasperFx;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Handlers;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Pii;
using Wolverine;
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

    public static IHostBuilder CriarHost(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
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

                // Protetor de PII: uma instância por escopo, exposta por duas portas,
                // compartilhando a sessão Marten ambiente do handler.
                opts.Services.AddScoped(sp => new ProtetorPiiAesGcm(sp.GetRequiredService<IDocumentSession>()));
                opts.Services.AddScoped<IProtetorPii>(sp => sp.GetRequiredService<ProtetorPiiAesGcm>());
                opts.Services.AddScoped<IServicoEsquecimento>(sp => sp.GetRequiredService<ProtetorPiiAesGcm>());

                // Handlers vivem neste assembly (classlib), não no de entrada (ADR-0043).
                opts.Discovery.IncludeAssembly(typeof(AbrirEditalHandler).Assembly);

                // Atomicidade write+evento: a middleware aplica SaveChanges e instala
                // os envelopes do outbox na mesma transação do append.
                opts.Policies.AutoApplyTransactions();

                // Nó único: determinístico para testes, sem eleição de liderança.
                opts.Durability.Mode = DurabilityMode.Solo;
            });
    }
}
