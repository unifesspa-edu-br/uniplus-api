namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Spike;

using System.Diagnostics.CodeAnalysis;

using Wolverine;

/// <summary>
/// SPIKE V3 — extension Wolverine que (a) inclui o assembly de testes na discovery
/// para registrar PublicarEditalSpikeHandler e (b) configura rota durável para
/// EditalPublicadoEvent. Aplicado durante build do host via DI
/// (services.AddSingleton&lt;IWolverineExtension, SpikeWolverineExtension&gt;()).
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via DI por Wolverine durante o build do host.")]
internal sealed class SpikeWolverineExtension : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Discovery.IncludeAssembly(typeof(SpikeWolverineExtension).Assembly);

        // Rota explícita para o domain event — sem isso EmptyMessageRouter descarta.
        // ToLocalQueue + UseDurableLocalQueues garante persistência do envelope no banco.
        options.PublishMessage<Unifesspa.UniPlus.Selecao.Domain.Events.EditalPublicadoEvent>()
            .ToLocalQueue("editais-spike");
        options.Policies.UseDurableLocalQueues();
    }
}
