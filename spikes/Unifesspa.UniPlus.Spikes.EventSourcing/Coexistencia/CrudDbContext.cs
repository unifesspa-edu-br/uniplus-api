using Microsoft.EntityFrameworkCore;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Coexistencia;

/// <summary>
/// DbContext EF Core do módulo CRUD da prova de coabitação. Vive em schema próprio
/// (<c>crud</c>); o outbox transacional do Wolverine usa a conexão/transação deste
/// contexto (ADR-0004), em paralelo ao Event Store do Marten no mesmo Postgres.
/// </summary>
public sealed class CrudDbContext(DbContextOptions<CrudDbContext> options) : DbContext(options)
{
    public DbSet<RegistroCrud> Registros => Set<RegistroCrud>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("crud");
        modelBuilder.Entity<RegistroCrud>(e =>
        {
            e.ToTable("registros");
            e.HasKey(x => x.Id);
            e.Property(x => x.Descricao).HasMaxLength(200);
        });
    }
}
