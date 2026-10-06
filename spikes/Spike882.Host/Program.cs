using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spike882.Agendamento;

using Wolverine;

// Uso: Spike882.Host <conexao> <duravel|sem-duravel> <agendar|ouvir|cadeia>
string conexao = args[0];
bool duravel = args[1] == "duravel";
string modo = args[2];

bool filasDuraveis = args.Length > 3 && args[3] == "filas-duraveis";

using IHost host = Hosts.Criar(conexao, duravel, caminhoProducao: modo.StartsWith("cadeia", StringComparison.Ordinal), filasLocaisDuraveis: filasDuraveis);
await host.StartAsync();
IMessageBus bus = host.Services.GetRequiredService<IMessageBus>();

if (modo == "agendar")
{
    await bus.ScheduleAsync(new ProbeAgendado("morto"), DateTimeOffset.UtcNow.AddSeconds(10));
    Console.WriteLine("AGENDADO");
    Console.Out.Flush();
}
else if (modo == "cadeia-lenta")
{
    // Sincronização do tick 2 fica lenta: o processo é morto no meio dela.
    Registro.TickLento = 2;
    Registro.SincronizacaoLentaMs = 30000;
    await bus.PublishAsync(new GatilhoAtomico(1));
    while (Registro.Contar("sync-inicio:2") == 0)
    {
        await Task.Delay(100);
    }

    Console.WriteLine("SYNC2_INICIADA");
    Console.Out.Flush();
}
else if (modo == "cadeia")
{
    await bus.PublishAsync(new GatilhoAtomico(1));
    while (Registro.Contar("gatilho-at:3") == 0)
    {
        await Task.Delay(100);
    }

    Console.WriteLine("TICK3");
    Console.Out.Flush();
}

await Task.Delay(Timeout.Infinite);
