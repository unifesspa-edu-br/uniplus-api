namespace Unifesspa.UniPlus.Ingresso.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;
using Application.Abstractions.Interfaces;
using Abstractions;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

public sealed class IngressoDbContext : DbContext, IIngressoUnitOfWork
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular (spike). Tabelas,
    /// índices e FKs deste DbContext vivem neste schema.
    /// </summary>
    public const string Schema = "ingresso";

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
        // Banco único, schema-por-módulo (spike monólito modular).
        modelBuilder.HasDefaultSchema(Schema);
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
