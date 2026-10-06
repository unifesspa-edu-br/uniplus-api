using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wolverine;

namespace Spike882.Agendamento;

public sealed record ProbeAgendado(string Id);

public sealed record Reagenda(string Id, bool FalharAposReagendar);

// Tick representa um "dia" do gatilho; o spike usa segundos para caber nos testes.
public sealed record Gatilho(int Tick, bool UsarClaim);

public sealed record Sincronizar(int Tick);

/// <summary>Registro estático dos eventos observados pelos handlers (um teste por vez).</summary>
public static class Registro
{
    private static ConcurrentQueue<(DateTimeOffset Quando, string Evento)> _eventos = new();

    public static bool FalharSincronizacao { get; set; }

    public static bool FalharGatilhoAntesDeAgendar { get; set; }

    public static int FalhasRestantesNoGatilho { get; set; }

    public static int TickLento { get; set; }

    public static int SincronizacaoLentaMs { get; set; }

    public static void Reiniciar()
    {
        _eventos = new();
        FalharSincronizacao = false;
        FalharGatilhoAntesDeAgendar = false;
        FalhasRestantesNoGatilho = 0;
        TickLento = 0;
        SincronizacaoLentaMs = 0;
    }

    public static void Registrar(string evento) => _eventos.Enqueue((DateTimeOffset.UtcNow, evento));

    public static int Contar(string exato) => _eventos.Count(e => e.Evento == exato);

    public static int ContarPrefixo(string prefixo) => _eventos.Count(e => e.Evento.StartsWith(prefixo, StringComparison.Ordinal));

    public static IReadOnlyList<string> Todos() =>
        _eventos.Select(e => $"{e.Quando:HH:mm:ss.fff} {e.Evento}").ToList();
}

/// <summary>
/// Claim mais simples possível: uma linha por tick com chave primária. Quem insere ganha;
/// o INSERT ... ON CONFLICT DO NOTHING devolve 0 linhas para a duplicata.
/// </summary>
public static class Claim
{
    public static string Conexao { get; set; } = string.Empty;

    public static async Task<bool> TentarAsync(int tick)
    {
        await using var conexao = new NpgsqlConnection(Conexao);
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand(
            "INSERT INTO spike_claim(tick) VALUES (@tick) ON CONFLICT DO NOTHING", conexao);
        comando.Parameters.AddWithValue("tick", tick);
        return await comando.ExecuteNonQueryAsync() == 1;
    }
}

public static class ProbeHandler
{
    public static void Handle(ProbeAgendado mensagem) => Registro.Registrar($"probe:{mensagem.Id}");
}

public static class ReagendaHandler
{
    // Primeira entrega: reagenda a própria mensagem e, se pedido, falha em seguida.
    public static async Task Handle(Reagenda mensagem, IMessageContext contexto)
    {
        int tentativa = Registro.ContarPrefixo($"reagenda:{mensagem.Id}:") + 1;
        Registro.Registrar($"reagenda:{mensagem.Id}:{tentativa}");

        if (tentativa == 1)
        {
            await contexto.ReScheduleCurrentAsync(DateTimeOffset.UtcNow.AddSeconds(3));

            if (mensagem.FalharAposReagendar)
            {
                throw new InvalidOperationException("falha simulada após reagendar");
            }
        }
    }
}

public static class GatilhoHandler
{
    // Cada gatilho agenda o próximo tick ANTES de publicar a sincronização do seu tick.
    // Com UsarClaim, só o primeiro gatilho de cada tick segue a cadeia; duplicatas viram noop.
    public static async Task Handle(Gatilho mensagem, IMessageBus bus)
    {
        if (mensagem.UsarClaim && !await Claim.TentarAsync(mensagem.Tick))
        {
            Registro.Registrar($"noop:{mensagem.Tick}");
            return;
        }

        Registro.Registrar($"gatilho:{mensagem.Tick}");

        if (Registro.FalhasRestantesNoGatilho > 0)
        {
            Registro.FalhasRestantesNoGatilho--;
            throw new InvalidOperationException("falha transitória no gatilho (simulado)");
        }

        if (Registro.FalharGatilhoAntesDeAgendar)
        {
            throw new InvalidOperationException("gatilho falhou antes de agendar o próximo (simulado)");
        }

        await bus.ScheduleAsync(new Gatilho(mensagem.Tick + 1, mensagem.UsarClaim), DateTimeOffset.UtcNow.AddSeconds(4));
        await bus.PublishAsync(new Sincronizar(mensagem.Tick));
    }
}

public static class SincronizarHandler
{
    public static async Task Handle(Sincronizar mensagem)
    {
        Registro.Registrar($"sync-inicio:{mensagem.Tick}");

        if (Registro.TickLento == mensagem.Tick)
        {
            await Task.Delay(Registro.SincronizacaoLentaMs);
        }

        Registro.Registrar($"sync:{mensagem.Tick}");

        if (Registro.FalharSincronizacao)
        {
            throw new InvalidOperationException("sincronização falhou (simulado)");
        }
    }
}

// ---- Caminho de produção: handler com EF + outbox na mesma transação --------------------

public sealed record GatilhoAtomico(int Tick);

public static class GatilhoAtomicoHandler
{
    // Reserva do tick e agendamento do próximo são gravados juntos: se o handler falha,
    // a reserva volta junto e o retry consegue reprocessar o mesmo tick.
    public static async Task Handle(GatilhoAtomico mensagem, SpikeDbContext banco, IMessageBus bus)
    {
        if (await banco.Ticks.AnyAsync(t => t.Tick == mensagem.Tick))
        {
            Registro.Registrar($"noop-at:{mensagem.Tick}");
            return;
        }

        banco.Ticks.Add(new TickReservado { Tick = mensagem.Tick });

        if (Registro.FalhasRestantesNoGatilho > 0)
        {
            Registro.FalhasRestantesNoGatilho--;
            throw new InvalidOperationException("falha transitória no gatilho atômico (simulado)");
        }

        await bus.ScheduleAsync(new GatilhoAtomico(mensagem.Tick + 1), DateTimeOffset.UtcNow.AddSeconds(4));
        await bus.PublishAsync(new Sincronizar(mensagem.Tick));
        Registro.Registrar($"gatilho-at:{mensagem.Tick}");
    }
}
