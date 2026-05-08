namespace Unifesspa.UniPlus.Portal.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Application.Abstractions.Interfaces;

public sealed class PortalDbContext : DbContext, IUnitOfWork
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortalDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
