using Microsoft.EntityFrameworkCore;

namespace Spike882.Agendamento;

/// <summary>Banco do caminho de produção: reserva de tick gravada na mesma transação do outbox.</summary>
public sealed class SpikeDbContext(DbContextOptions<SpikeDbContext> opcoes) : DbContext(opcoes)
{
    public DbSet<TickReservado> Ticks => Set<TickReservado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TickReservado>(e =>
        {
            e.ToTable("tick_reservado");
            e.HasKey(x => x.Tick);
            e.Property(x => x.Tick).ValueGeneratedNever();
        });
    }
}

public sealed class TickReservado
{
    public int Tick { get; set; }
}
