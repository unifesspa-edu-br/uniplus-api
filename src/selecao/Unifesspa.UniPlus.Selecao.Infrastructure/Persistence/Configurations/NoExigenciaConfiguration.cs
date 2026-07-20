namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="NoExigencia"/> (Story #920) — árvore de satisfação:
/// coleção PLANA de todos os nós do processo (não só raízes), FK obrigatória para
/// <see cref="Entities.ProcessoSeletivo"/> (configurada em <c>ProcessoSeletivoConfiguration</c>,
/// mesmo padrão de <c>DocumentosExigidos</c>/<c>CronogramaFases</c>) — <c>Raizes</c> é projeção em
/// memória (<c>NoPaiId == null</c>) e <c>Filhos</c> é populado pelo relationship fix-up do EF a
/// partir da self-FK <see cref="NoExigencia.NoPaiId"/>, sem <c>Include/ThenInclude</c> recursivo.
/// </summary>
public sealed class NoExigenciaConfiguration : IEntityTypeConfiguration<NoExigencia>
{
    private const int ConsequenciaMaxLength = 30;

    public void Configure(EntityTypeBuilder<NoExigencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Defesa em profundidade (a validação real é de domínio, NoExigencia.CriarGrupo):
        // campos de folha/grupo coerentes com o tipo, e as duas semânticas de quantidadeMinima
        // (NULL em folha/E, >=1 em OU/N-de) e consequência (só OU/N-de).
        builder.ToTable("nos_exigencia", t =>
        {
            t.HasCheckConstraint(
                "ck_nos_exigencia_tipo_campos_coerentes",
                "(tipo = 1 AND documento_exigido_id IS NOT NULL AND quantidade_minima IS NULL AND consequencia IS NULL) OR " +
                "(tipo = 2 AND documento_exigido_id IS NULL AND quantidade_minima IS NULL AND consequencia IS NULL) OR " +
                "(tipo = 3 AND documento_exigido_id IS NULL AND quantidade_minima IS NOT NULL)");
            t.HasCheckConstraint("ck_nos_exigencia_id_diferente_de_no_pai_id", "id <> no_pai_id");
            t.HasCheckConstraint("ck_nos_exigencia_ordem_nao_negativa", "ordem >= 0");
            t.HasCheckConstraint(
                "ck_nos_exigencia_quantidade_minima_positiva", "quantidade_minima IS NULL OR quantidade_minima >= 1");
        });

        builder.HasKey(n => n.Id);
        // Chave Guid v7 do domínio (EntityBase) — ValueGeneratedNever, mesmo padrão de
        // DocumentoExigido/FaseCronograma.
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Ordem).IsRequired();
        builder.Property(n => n.Tipo).HasConversion<int>().IsRequired();
        builder.Property(n => n.Consequencia).HasMaxLength(ConsequenciaMaxLength);

        // Self-FK — árvore. Restrict: a exclusão de um nó é sempre disparada pela coleção do
        // PROCESSO (ProcessoSeletivoId, configurada em ProcessoSeletivoConfiguration como
        // Cascade, mesmo padrão de DocumentosExigidos), nunca por cascade via NoPaiId — evita
        // múltiplos caminhos de cascade em self-referencing FK no Postgres.
        builder.HasOne<NoExigencia>()
            .WithMany(n => n.Filhos)
            .HasForeignKey(n => n.NoPaiId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(n => n.Filhos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // 1 folha por DocumentoExigido — FK real (Restrict: mesmo raciocínio de
        // DocumentoExigido.ExigidoNaFaseId, a checagem de negócio é check-then-act e a
        // constraint é a defesa atômica).
        builder.HasOne(n => n.DocumentoExigido)
            .WithMany()
            .HasForeignKey(n => n.DocumentoExigidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.DocumentoExigidoId)
            .IsUnique()
            .HasFilter("documento_exigido_id IS NOT NULL")
            .HasDatabaseName("ux_nos_exigencia_documento_exigido_id");

        // Sem colisão de posição entre irmãos (NoPaiId IS NOT NULL) nem entre raízes do
        // mesmo processo (NoPaiId IS NULL) — dois índices filtrados porque NULL do Postgres
        // já é distinto por padrão (a 2ª condição não precisa de AreNullsDistinct).
        builder.HasIndex(n => new { n.NoPaiId, n.Ordem })
            .IsUnique()
            .HasFilter("no_pai_id IS NOT NULL")
            .HasDatabaseName("ux_nos_exigencia_irmaos_ordem");

        builder.HasIndex(n => new { n.ProcessoSeletivoId, n.Ordem })
            .IsUnique()
            .HasFilter("no_pai_id IS NULL")
            .HasDatabaseName("ux_nos_exigencia_raiz_ordem");

        // Base legal 1:N PRÓPRIA de grupo OU/N-de com consequência (Story #920) — substituível
        // por inteiro junto com o próprio nó, mesmo padrão de DocumentoExigido.BasesLegais.
        builder.HasMany(n => n.BasesLegais)
            .WithOne()
            .HasForeignKey(b => b.NoExigenciaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(n => n.BasesLegais)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
