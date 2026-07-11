namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// DbContext do módulo Publicações — o registro central dos atos normativos
/// publicados por Reitoria, CEPS e CRCA (ADR-0105).
///
/// Hospeda o cadastro de tipos de ato, o ato normativo append-only, o vínculo
/// genérico ato↔entidade e o cache de Idempotency-Key (ADR-0027) adjacente. O
/// módulo não conhece ProcessoSeletivo, Chamada nem configuração de certame —
/// nenhuma coluna, nenhuma chave estrangeira desses conceitos entra aqui, e a
/// ausência é travada por fitness test contra o banco real.
/// </summary>
public sealed class PublicacoesDbContext : DbContext, IPublicacoesUnitOfWork
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular (ADR-0097). Tabelas,
    /// índices e FKs deste DbContext vivem neste schema.
    /// </summary>
    public const string Schema = "publicacoes";

    public PublicacoesDbContext(DbContextOptions<PublicacoesDbContext> options) : base(options)
    {
    }

    public DbSet<TipoAtoPublicado> TiposAtoPublicado => Set<TipoAtoPublicado>();

    /// <summary>
    /// Atos normativos publicados — registro central e append-only (ADR-0063/0105).
    /// Só recebe <c>INSERT</c>; <c>UPDATE</c>/<c>DELETE</c> são bloqueados por trigger.
    /// </summary>
    public DbSet<AtoNormativo> AtosNormativos => Set<AtoNormativo>();

    /// <summary>
    /// Vínculos genéricos ato ↔ entidade (ADR-0105) — o par opaco que serve a consulta
    /// unificada sem que o módulo conheça os domínios.
    /// </summary>
    public DbSet<VinculoAtoEntidade> VinculosAtoEntidade => Set<VinculoAtoEntidade>();

    /// <summary>
    /// Vagas de linhagem única por objeto (ADR-0107) — a chave que o banco trava para
    /// que um objeto não seja tratado por duas linhagens de atos do mesmo tipo único.
    /// </summary>
    public DbSet<LinhagemUnicaPorObjeto> LinhagensUnicasPorObjeto => Set<LinhagemUnicaPorObjeto>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no schema do módulo para permitir
    /// gravação adjacente no outbox.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        // Banco único, schema-por-módulo.
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublicacoesDbContext).Assembly);
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
