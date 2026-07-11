namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class VinculoAtoEntidadeConfiguration : IEntityTypeConfiguration<VinculoAtoEntidade>
{
    private const int EntidadeTipoMaxLength = 60;

    public void Configure(EntityTypeBuilder<VinculoAtoEntidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "vinculo_ato_entidade",
            t =>
            {
                // Forma canônica do rótulo (a factory já recusa). O módulo não valida o
                // VALOR — não há lista de tipos permitidos, e não pode haver: o rótulo é
                // opaco (ADR-0105). O que se exige é grafia única, sem a qual a mesma
                // entidade se partiria em duas na consulta.
                t.HasCheckConstraint(
                    "ck_vinculo_ato_entidade_tipo_formato",
                    "entidade_tipo ~ '^[A-Z0-9]+(_[A-Z0-9]+)*$'");

                // Um Guid.Empty não designa objeto algum — defesa contra insert cru.
                t.HasCheckConstraint(
                    "ck_vinculo_ato_entidade_id_nao_zero",
                    "entidade_id <> '00000000-0000-0000-0000-000000000000'");
            });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.AtoId).IsRequired();
        builder.Property(v => v.EntidadeTipo).HasMaxLength(EntidadeTipoMaxLength).IsRequired();
        builder.Property(v => v.EntidadeId).IsRequired();

        // A ÚNICA chave estrangeira do vínculo, e ela aponta para o próprio ato
        // (contraprova estrutural da story #801). Não há — e não pode haver — FK para
        // processo_seletivo, chamada ou aplicacao_prova: o módulo não conhece esses
        // conceitos, e a ausência é travada por fitness test contra o banco real.
        // Cascade: o vínculo é parte do ato; ambos são append-only, e nenhum DELETE
        // chega até aqui (o trigger o bloqueia antes).
        builder.HasOne<AtoNormativo>()
            .WithMany(a => a.Vinculos)
            .HasForeignKey(v => v.AtoId)
            .HasConstraintName("fk_vinculo_ato_entidade_ato")
            .OnDelete(DeleteBehavior.Cascade);

        // Único por trio (ato, tipo da entidade, id da entidade): vincular a mesma
        // entidade duas vezes ao mesmo ato não acrescenta vínculo nenhum.
        builder.HasIndex(v => new { v.AtoId, v.EntidadeTipo, v.EntidadeId })
            .IsUnique()
            .HasDatabaseName("ux_vinculo_ato_entidade_trio");

        // Suporte à consulta "todos os atos desta entidade": o predicado é
        // (entidade_tipo, entidade_id), e o ato_id fecha o EXISTS sem tocar a heap.
        builder.HasIndex(v => new { v.EntidadeTipo, v.EntidadeId, v.AtoId })
            .HasDatabaseName("ix_vinculo_ato_entidade_objeto");
    }
}
