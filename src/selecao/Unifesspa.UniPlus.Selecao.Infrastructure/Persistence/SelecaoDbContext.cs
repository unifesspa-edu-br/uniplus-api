namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Domain.Entities;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

public sealed class SelecaoDbContext : DbContext, ISelecaoUnitOfWork
{
    /// <summary>
    /// Schema do módulo no banco único do monólito modular. Tabelas,
    /// índices, FKs e idempotency_cache deste DbContext vivem neste schema.
    /// </summary>
    public const string Schema = "selecao";

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
    /// Entidades de configuração do agregado <see cref="ProcessoSeletivo"/>
    /// (Story #758). Expostas como DbSet para consultas e seeds de teste; a
    /// escrita passa sempre pela raiz via <c>IProcessoSeletivoRepository</c>.
    /// </summary>
    public DbSet<EtapaProcesso> EtapasProcesso => Set<EtapaProcesso>();
    public DbSet<OfertaAtendimentoEspecializado> OfertasAtendimentoEspecializado => Set<OfertaAtendimentoEspecializado>();

    /// <summary>
    /// Distribuição de vagas por oferta de curso (Story #773, modelagem P-A):
    /// os inputs (VO_base, PR, referência à regra tipada, snapshot da
    /// referência demográfica) e as modalidades selecionadas. O
    /// <c>QuadroDeVagas</c> (output derivado) não é modelado aqui.
    /// </summary>
    public DbSet<ConfiguracaoDistribuicaoVagas> ConfiguracoesDistribuicaoVagas => Set<ConfiguracaoDistribuicaoVagas>();
    public DbSet<ModalidadeSelecionada> ModalidadesSelecionadas => Set<ModalidadeSelecionada>();

    /// <summary>
    /// Bônus regional (RN05) e critérios de desempate (Story #774, modelagem
    /// P-B §2.5/§2.6) — ambos por referência ao <c>rol_de_regras</c>.
    /// </summary>
    public DbSet<ConfiguracaoBonusRegional> ConfiguracoesBonusRegional => Set<ConfiguracaoBonusRegional>();
    public DbSet<CriterioDesempate> CriteriosDesempate => Set<CriterioDesempate>();

    /// <summary>
    /// Classificação (15º bloco canônico, Story #775, modelagem P-B §2.1) —
    /// compõe por referência a fórmula, precisão, eliminação e ordem de
    /// alocação.
    /// </summary>
    public DbSet<ConfiguracaoClassificacao> ConfiguracoesClassificacao => Set<ConfiguracaoClassificacao>();
    public DbSet<RegraEliminacao> RegrasEliminacao => Set<RegraEliminacao>();

    /// <summary>
    /// Biblioteca <c>rol_de_regras</c> (Story #772) — regras tipadas e
    /// versionadas que a configuração do Processo Seletivo referencia
    /// (<c>codigo</c>+<c>versao</c>+<c>hash</c>), congeladas no snapshot de
    /// publicação (RN08). Seed-governada e append-only (não é CRUD de admin):
    /// a leitura passa por <c>IRegraCatalogoReader</c> e a única escrita é o
    /// seed da migration.
    /// </summary>
    public DbSet<RegraCatalogo> RolDeRegras => Set<RegraCatalogo>();

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
        // Banco único, schema-por-módulo.
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SelecaoDbContext).Assembly);
        // Configurações cross-cutting de Infrastructure.Core (ex.: idempotency_cache).
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
