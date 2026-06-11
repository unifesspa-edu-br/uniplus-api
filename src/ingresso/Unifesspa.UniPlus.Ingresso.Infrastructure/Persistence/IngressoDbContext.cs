namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;
using Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

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
        // Convenção global de soft-delete (issue #629): aplica `!IsDeleted` a todo
        // tipo ISoftDeletable, após os ApplyConfigurations registrarem os tipos.
        modelBuilder.AplicarFiltroGlobalSoftDelete();
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
