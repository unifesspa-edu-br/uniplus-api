namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// <see cref="DbContext"/> do módulo Geo — banco isolado <c>uniplus_geo</c>,
/// naming snake_case (ADR-0054), read-mostly. Estreia o eixo geoespacial:
/// coordenadas mapeadas para <c>geography(Point,4326)</c> via NetTopologySuite
/// (ADR-0091). Reference data do Geo não tem soft-delete (ADR-0092) — as
/// entidades derivam de <c>EntityBase</c> puro.
/// </summary>
/// <remarks>
/// Em V1 hospeda a entidade-sonda transitória
/// (<see cref="PontoReferenciaSonda"/>, removida na Story de entidades reais) e
/// <c>idempotency_cache</c> (entries cifradas at-rest). As entidades de
/// localidade (Pais, Estado, Cidade, …) entram nas Stories de domínio do Epic.
/// </remarks>
public sealed class GeoDbContext : DbContext, IUnitOfWork
{
    public GeoDbContext(DbContextOptions<GeoDbContext> options)
        : base(options)
    {
    }

    /// <summary>Sonda transitória que valida o mapeamento PostGIS fim-a-fim (ADR-0091).</summary>
    public DbSet<PontoReferenciaSonda> PontosReferenciaSonda => Set<PontoReferenciaSonda>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do módulo
    /// para permitir gravação adjacente no outbox.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GeoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        // Convenção global de soft-delete: aplica `!IsDeleted` a todo tipo
        // ISoftDeletable. Nenhuma entidade do Geo é ISoftDeletable (ADR-0092),
        // então a convenção é no-op aqui — mantida por simetria com os demais
        // módulos e para cobrir idempotency entry (que também não é soft-delete).
        modelBuilder.AplicarFiltroGlobalSoftDelete();
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
