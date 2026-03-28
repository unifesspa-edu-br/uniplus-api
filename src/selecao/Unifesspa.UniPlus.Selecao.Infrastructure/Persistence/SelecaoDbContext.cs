namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.SharedKernel.Domain.Interfaces;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelecaoDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
