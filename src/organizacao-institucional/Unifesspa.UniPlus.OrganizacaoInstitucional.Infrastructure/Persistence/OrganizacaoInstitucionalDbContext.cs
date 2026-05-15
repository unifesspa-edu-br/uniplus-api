namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// <see cref="DbContext"/> do módulo OrganizacaoInstitucional — banco
/// <c>uniplus_organizacao</c>, naming snake_case (ADR-0054). Hospeda a
/// agregada <see cref="AreaOrganizacional"/> e o cache de Idempotency-Key
/// (ADR-0027) adjacente, permitindo gravação atômica em outbox.
/// </summary>
public sealed class OrganizacaoInstitucionalDbContext : DbContext, IUnitOfWork
{
    public OrganizacaoInstitucionalDbContext(DbContextOptions<OrganizacaoInstitucionalDbContext> options)
        : base(options)
    {
    }

    public DbSet<AreaOrganizacional> AreasOrganizacionais => Set<AreaOrganizacional>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do agregado
    /// para permitir gravação adjacente no outbox; entries cifradas at-rest
    /// via <c>IUniPlusEncryptionService</c>.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizacaoInstitucionalDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
