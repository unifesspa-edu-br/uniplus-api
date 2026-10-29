namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// <see cref="DbContext"/> do módulo Autorização, no schema <c>autorizacao</c> do banco único,
/// naming snake_case (ADR-0054). Hospeda os perfis de acesso, as concessões, as instalações dos primeiros gestores e o cache de Idempotency-Key
/// (ADR-0027) adjacente, que permite gravação atômica com o outbox.
/// </summary>
public sealed class AutorizacaoDbContext : DbContext, IAutorizacaoUnitOfWork
{
    /// <summary>Schema do módulo: tabelas, índices, FKs e idempotency_cache deste DbContext.</summary>
    public const string Schema = "autorizacao";

    public AutorizacaoDbContext(DbContextOptions<AutorizacaoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Perfil> Perfis => Set<Perfil>();

    public DbSet<Concessao> Concessoes => Set<Concessao>();

    public DbSet<Instalacao> Instalacoes => Set<Instalacao>();

    public DbSet<TentativaDeInstalacao> TentativasDeInstalacao => Set<TentativaDeInstalacao>();

    /// <summary>Cache de Idempotency-Key (ADR-0027), cifrado at-rest.</summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutorizacaoDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        modelBuilder.AplicarFiltroGlobalSoftDelete();

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void DescartarAlteracoesNaoSalvas() => ChangeTracker.Clear();
}
