namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// <see cref="DbContext"/> do módulo Configuracao — banco
/// <c>uniplus_configuracao</c>, naming snake_case (ADR-0054). Hospeda os
/// cadastros <see cref="Campus"/> e <see cref="LocalOferta"/> (UNI-REQ #587) e o
/// cache de Idempotency-Key (ADR-0027) adjacente. A <c>Cidade</c> é dado do
/// módulo <c>Geo</c> (ADR-0090) — referenciada por código + display cache, sem
/// entidade/tabela própria aqui.
/// </summary>
public sealed class ConfiguracaoDbContext : DbContext, IUnitOfWork
{
    public ConfiguracaoDbContext(DbContextOptions<ConfiguracaoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Campus> Campi => Set<Campus>();

    public DbSet<LocalOferta> LocaisOferta => Set<LocalOferta>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do módulo
    /// para permitir gravação adjacente no outbox.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConfiguracaoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
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
