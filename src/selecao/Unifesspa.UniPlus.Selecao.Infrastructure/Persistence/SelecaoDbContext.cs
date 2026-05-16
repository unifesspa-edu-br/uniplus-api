namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

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

    /// <summary>
    /// Catálogo data-driven de regras legais (Story #460, ADR-0058). O CRUD
    /// admin entra em #461; em V1 esta DbSet é populada via factory direta
    /// para testes e seeds.
    /// </summary>
    public DbSet<ObrigatoriedadeLegal> ObrigatoriedadesLegais => Set<ObrigatoriedadeLegal>();

    /// <summary>
    /// Histórico append-only de mutações de <see cref="ObrigatoriedadesLegais"/>
    /// (CA-03). Linha inserida na mesma transação do save da regra pelo
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c>.
    /// </summary>
    public DbSet<ObrigatoriedadeLegalHistorico> ObrigatoriedadeLegalHistorico =>
        Set<ObrigatoriedadeLegalHistorico>();

    /// <summary>
    /// Snapshots de governança capturados por <c>Edital.Publicar()</c>
    /// (ADR-0057 §"Pattern 1"). Em #460 a tabela é criada vazia; #462
    /// é responsável pelo INSERT.
    /// </summary>
    public DbSet<EditalGovernanceSnapshot> EditalGovernanceSnapshots =>
        Set<EditalGovernanceSnapshot>();

    /// <summary>
    /// Cache de Idempotency-Key (ADR-0027). Vive no mesmo banco do agregado
    /// para permitir gravação adjacente no outbox; entries cifradas at-rest
    /// via <c>IUniPlusEncryptionService</c>.
    /// </summary>
    public DbSet<IdempotencyEntry> IdempotencyEntries => Set<IdempotencyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelecaoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (ex.: idempotency_cache).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyEntry).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
