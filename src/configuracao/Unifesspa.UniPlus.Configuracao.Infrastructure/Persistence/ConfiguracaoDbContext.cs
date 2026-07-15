namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
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
public sealed class ConfiguracaoDbContext : DbContext, IConfiguracaoUnitOfWork
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular. Todas as
    /// tabelas e o histórico de migrations deste DbContext vivem neste schema,
    /// isolando o módulo dos demais no mesmo banco.
    /// </summary>
    public const string Schema = "configuracao";

    public ConfiguracaoDbContext(DbContextOptions<ConfiguracaoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Campus> Campi => Set<Campus>();

    public DbSet<LocalOferta> LocaisOferta => Set<LocalOferta>();

    public DbSet<ReferenciaReservaDemografica> ReferenciasReservaDemografica => Set<ReferenciaReservaDemografica>();

    public DbSet<PesoAreaEnem> PesosAreaEnem => Set<PesoAreaEnem>();

    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();

    public DbSet<CondicaoAtendimentoEspecializado> CondicoesAtendimento => Set<CondicaoAtendimentoEspecializado>();

    public DbSet<RecursoAcessibilidade> RecursosAcessibilidade => Set<RecursoAcessibilidade>();

    public DbSet<TipoDeficiencia> TiposDeficiencia => Set<TipoDeficiencia>();

    public DbSet<Modalidade> Modalidades => Set<Modalidade>();

    public DbSet<FaseCanonica> FasesCanonicas => Set<FaseCanonica>();

    public DbSet<TipoBanca> TiposBanca => Set<TipoBanca>();

    public DbSet<Curso> Cursos => Set<Curso>();

    public DbSet<OfertaCurso> OfertasCurso => Set<OfertaCurso>();

    /// <summary>
    /// Catálogo seed-governado do vocabulário fechado de fatos do candidato
    /// (UNI-REQ-0077, ADR-0111). Metadado de classificação, sem PII.
    /// </summary>
    public DbSet<FatoCandidato> FatosCandidato => Set<FatoCandidato>();

    /// <summary>
    /// Grafo de precedências entre fases canônicas (UNI-REQ-0064, story #851):
    /// CRUD-administrado e seed-governado com as seis arestas estruturais de §3.3.
    /// </summary>
    public DbSet<PrecedenciaFase> PrecedenciasFase => Set<PrecedenciaFase>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do módulo
    /// para permitir gravação adjacente no outbox.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        // Banco único, schema-por-módulo: fixa o schema
        // default do modelo — tabelas, índices, FKs e idempotency_cache deste
        // módulo passam a ser qualificados por `configuracao`.
        modelBuilder.HasDefaultSchema(Schema);
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
