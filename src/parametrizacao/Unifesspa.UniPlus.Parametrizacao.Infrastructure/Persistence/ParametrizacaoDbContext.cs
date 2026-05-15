namespace Unifesspa.UniPlus.Parametrizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// <see cref="DbContext"/> do módulo Parametrizacao — banco
/// <c>uniplus_parametrizacao</c>, naming snake_case (ADR-0054). Em V1 hospeda
/// apenas <c>idempotency_cache</c> (entries cifradas at-rest via
/// <c>IUniPlusEncryptionService</c>); entidades de catálogo (Modalidade,
/// NecessidadeEspecial, TipoDocumento, Endereco) entram em F2.
/// </summary>
public sealed class ParametrizacaoDbContext : DbContext, IUnitOfWork
{
    public ParametrizacaoDbContext(DbContextOptions<ParametrizacaoDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do módulo
    /// para permitir gravação adjacente no outbox.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParametrizacaoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
