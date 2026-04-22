namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Ingresso.Domain.Entities;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public sealed class IngressoDbContext : DbContext, IUnitOfWork
{
    public IngressoDbContext(DbContextOptions<IngressoDbContext> options) : base(options)
    {
    }

    public DbSet<Chamada> Chamadas => Set<Chamada>();
    public DbSet<Convocacao> Convocacoes => Set<Convocacao>();
    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<DocumentoMatricula> DocumentosMatricula => Set<DocumentoMatricula>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IngressoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
