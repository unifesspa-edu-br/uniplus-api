namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

public sealed class SelecaoDbContext : DbContext, IUnitOfWork
{
    public SelecaoDbContext(DbContextOptions<SelecaoDbContext> options) : base(options)
    {
    }

    public DbSet<Edital> Editais => Set<Edital>();
    public DbSet<Etapa> Etapas => Set<Etapa>();
    public DbSet<Cota> Cotas => Set<Cota>();
    public DbSet<Inscricao> Inscricoes => Set<Inscricao>();
    public DbSet<Candidato> Candidatos => Set<Candidato>();
    public DbSet<ProcessoSeletivo> ProcessosSeletivos => Set<ProcessoSeletivo>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do agregado
    /// para permitir gravação adjacente no outbox; entries cifradas at-rest
    /// via <c>IUniPlusEncryptionService</c>.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelecaoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (ex.: idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
