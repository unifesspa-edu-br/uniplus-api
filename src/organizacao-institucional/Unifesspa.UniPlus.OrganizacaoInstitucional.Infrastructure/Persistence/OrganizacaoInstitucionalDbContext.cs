namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// <see cref="DbContext"/> do módulo OrganizacaoInstitucional — banco
/// <c>uniplus_organizacao</c>, naming snake_case (ADR-0054). Hospeda a
/// agregada <see cref="Unidade"/> e o cache de Idempotency-Key
/// (ADR-0027) adjacente, permitindo gravação atômica em outbox.
/// </summary>
public sealed class OrganizacaoInstitucionalDbContext : DbContext, IUnitOfWork
{
    public OrganizacaoInstitucionalDbContext(DbContextOptions<OrganizacaoInstitucionalDbContext> options)
        : base(options)
    {
    }

    public DbSet<Unidade> Unidades => Set<Unidade>();

    public DbSet<UnidadeIdentificadorHistorico> UnidadesIdentificadoresHistorico =>
        Set<UnidadeIdentificadorHistorico>();

    public DbSet<Instituicao> Instituicoes => Set<Instituicao>();

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
        // Convenção global de soft-delete (issue #629): aplica `!IsDeleted` a todo
        // tipo ISoftDeletable, após os ApplyConfigurations registrarem os tipos.
        // UnidadeIdentificadorHistorico não implementa ISoftDeletable → não filtra.
        modelBuilder.AplicarFiltroGlobalSoftDelete();

        // Mapeia PgFunctions.ImmutableUnaccent → immutable_unaccent(text) do banco
        // (criada pela migration AddSearchExtensionsGin). Usada nas queries de busca
        // textual da Unidade para remover diacríticos server-side (issue #640).
        modelBuilder.HasDbFunction(
            typeof(PgFunctions).GetMethod(nameof(PgFunctions.ImmutableUnaccent))!)
            .HasName("immutable_unaccent");

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
