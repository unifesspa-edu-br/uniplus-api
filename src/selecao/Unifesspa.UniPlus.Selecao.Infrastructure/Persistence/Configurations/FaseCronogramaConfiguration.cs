namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Canonicalization;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Configuração EF Core de <see cref="FaseCronograma"/> (Story #851) — entidade filha
/// do agregado <see cref="ProcessoSeletivo"/>. Os limites de comprimento espelham
/// <c>LimitesDoEnvelope</c> (constantes duplicadas por convenção do repo, verificadas
/// pelo fitness <c>LimitesDoEnvelopeBatemComOSchemaTests</c>).
/// </summary>
public sealed class FaseCronogramaConfiguration : IEntityTypeConfiguration<FaseCronograma>
{
    private const int CodigoMaxLength = 60;
    private const int UnidadeSiglaMaxLength = 50;
    private const int UnidadeNomeMaxLength = 250;

    public void Configure(EntityTypeBuilder<FaseCronograma> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("fases_cronograma", tabela =>
        {
            tabela.HasComment("Fases e períodos do cronograma de cada processo seletivo.");
            tabela.HasCheckConstraint("ck_fases_cronograma_unidade_completa",
                "(unidade_responsavel_origem_id IS NULL AND unidade_responsavel_codigo IS NULL AND unidade_responsavel_sigla IS NULL AND unidade_responsavel_nome IS NULL) "
                + "OR (unidade_responsavel_origem_id IS NOT NULL AND unidade_responsavel_codigo IS NOT NULL AND unidade_responsavel_sigla IS NOT NULL AND unidade_responsavel_nome IS NOT NULL)");
            tabela.HasCheckConstraint("ck_fases_cronograma_periodo_em_ordem", "inicio IS NULL OR fim IS NULL OR inicio <= fim");
        });
        builder.HasKey(f => f.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever para o EF
        // tratar a chave como fornecida pela aplicação (evita UPDATE de filho novo
        // ao reconfigurar o agregado tracked). Convenção do repo.
        builder.Property(f => f.Id).ValueGeneratedNever().HasComment("Identificador da fase no cronograma.");

        builder.Property(f => f.Ordem).HasComment("Posição da fase no cronograma.").IsRequired();
        builder.Property(f => f.NoMesmoMomentoDaAnterior).HasComment("Indica se a fase ocorre no mesmo momento da fase anterior.").HasColumnName("no_mesmo_momento_da_anterior").HasDefaultValue(false).IsRequired();
        builder.Property(f => f.FaseCanonicaOrigemId).HasColumnName("tipo_fase_origem_id").HasComment("Identificador da fase de origem copiado ao definir o cronograma.").IsRequired();
        builder.Property(f => f.Codigo).HasComment("Código da fase de origem copiado ao definir o cronograma. Exemplo: HOMOLOGACAO_INSCRICOES.").HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(f => f.TipoFaseNome).HasColumnName("tipo_fase_nome").HasMaxLength(200).IsRequired()
            .HasComment("Nome do tipo de fase copiado do cadastro ao definir o cronograma.");
        builder.Property(f => f.OrigemData).HasConversion<int>().IsRequired()
            .HasComment($"Momento em que as datas da fase são definidas. Códigos: {string.Join(", ", Enum.GetValues<OrigemDataFase>().Select(static valor => $"{(int)valor} ({valor})"))}.");

        builder.OwnsOne(f => f.UnidadeResponsavel, unidade =>
        {
            unidade.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);
            unidade.Property(u => u.OrigemId).HasColumnName("unidade_responsavel_origem_id")
                .HasComment("Identificador da unidade responsável, copiado quando definida.");
            unidade.Property(u => u.Codigo).HasColumnName("unidade_responsavel_codigo").HasMaxLength(LimitesDoEnvelope.UnidadeAdministradoraCodigo).IsRequired()
                .HasComment("Código da unidade responsável no cadastro, copiado ao salvar a fase.");
            unidade.Property(u => u.Sigla).HasColumnName("unidade_responsavel_sigla").HasMaxLength(UnidadeSiglaMaxLength).IsRequired()
                .HasComment("Sigla da unidade responsável, copiada quando definida.");
            unidade.Property(u => u.Nome).HasColumnName("unidade_responsavel_nome").HasMaxLength(UnidadeNomeMaxLength).IsRequired()
                .HasComment("Nome da unidade responsável, copiado quando definida.");
        });

        builder.Property(f => f.Chave)
            .HasConversion(chave => chave.Valor, valor => ChaveLocal.Criar(valor).Value)
            .HasColumnName("chave_local").HasMaxLength(ChaveLocal.ComprimentoMaximo).IsRequired()
            .HasComment("Identificador que a parte guarda dentro da configuração publicada; único no processo.");
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Chave })
            .IsUnique().HasDatabaseName("ux_fases_cronograma_processo_chave_local");
        builder.Property(f => f.ProcessoSeletivoId).HasComment("Processo seletivo ao qual a fase pertence.");
        builder.Property(f => f.AgrupaEtapas).HasComment("Indica se a fase agrupa etapas de avaliação.");
        builder.Property(f => f.PermiteComplementacao).HasComment("Indica se a fase permite complementar informações.");
        builder.Property(f => f.ColetaInscricao).HasComment("Indica se a fase recebe inscrições.");
        builder.Property(f => f.ColetaSolicitacaoIsencao).HasComment("Indica se a fase recebe pedidos de isenção da taxa.");
        builder.Property(f => f.Inicio).HasComment("Início do período da fase, quando definido.");
        builder.Property(f => f.Fim).HasComment("Fim do período da fase, quando definido.");
        builder.MapearRegraDeHomologacao(f => f.RegraDeHomologacao);

        builder.Property(f => f.EmiteParecerIndividual).HasComment("Indica se a fase emite parecer individual para cada candidato.").IsRequired();
        builder.MapearTiposDeDecisao();

        // Ordem e fase canônica únicas por processo — a rejeição em
        // ProcessoSeletivo.DefinirCronogramaFases é check-then-act não-atômico; a
        // constraint do banco é a defesa realmente atômica (mesmo padrão de
        // CriterioDesempateConfiguration).
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_fases_cronograma_processo_ordem");
        builder.HasIndex(f => new { f.ProcessoSeletivoId, f.FaseCanonicaOrigemId })
            .IsUnique()
            .HasDatabaseName("ux_fases_cronograma_processo_tipo_fase");

        builder.HasMany(f => f.Produtos)
            .WithOne()
            .HasForeignKey(p => p.FaseCronogramaId)
            .HasConstraintName("fk_publicacoes_previstas_da_fase_fase")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(f => f.BancasRequeridas)
            .WithOne()
            .HasForeignKey("FaseCronogramaId", "ProcessoSeletivoId")
            .HasPrincipalKey(nameof(FaseCronograma.Id), nameof(FaseCronograma.ProcessoSeletivoId))
            .HasConstraintName("fk_bancas_requeridas_fase")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.RegraRecurso)
            .WithOne()
            .HasForeignKey<RegraRecursoFase>(r => r.FaseCronogramaId)
            .HasConstraintName("fk_recursos_configurados_da_fase_fase")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(f => f.Produtos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(f => f.BancasRequeridas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
