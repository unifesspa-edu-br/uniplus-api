namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class UnidadeConfiguration : IEntityTypeConfiguration<Unidade>
{
    public void Configure(EntityTypeBuilder<Unidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("unidade");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).HasMaxLength(250).IsRequired();
        builder.Property(u => u.Alias).HasMaxLength(100);
        builder.Property(u => u.Slug)
            .HasConversion(new SlugValueConverter())
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(u => u.Sigla).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(u => u.Origem).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Índice de busca desnormalizado (issue #640): coluna `text` mantida pelo
        // agregado (acento/caixa-insensível). Sem HasMaxLength — é a concatenação
        // normalizada de nome+sigla+codigo+slug+alias. Filtro server-side via
        // u.BuscaNormalizada.Contains(termo). Universo pequeno (dezenas) dispensa
        // índice GIN trigram por ora; pg_trgm já está provisionado se escalar.
        builder.Property(u => u.BuscaNormalizada).IsRequired();
        builder.Property(u => u.UnidadeAcademica).IsRequired();
        builder.Property(u => u.VigenciaInicio).IsRequired();
        builder.Property(u => u.VigenciaFim);

        // Auditoria (IAuditableEntity)
        builder.Property(u => u.CreatedBy).HasMaxLength(255);
        builder.Property(u => u.UpdatedBy).HasMaxLength(255);

        // Hierarquia: auto-referência intra-banco (ADR-0054)
        builder.HasOne<Unidade>()
            .WithMany()
            .HasForeignKey(u => u.UnidadeSuperiorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Histórico de identificadores (append-only): NÃO cascatear (issue #629).
        // O histórico não implementa ISoftDeletable; com Cascade, remover a Unidade
        // marcaria os históricos carregados (ObterPorIdAsync faz Include) como Deleted
        // e — como o SoftDeleteInterceptor só converte ISoftDeletable — eles sofreriam
        // hard-delete físico, destruindo a trilha de auditoria.
        //
        // ClientNoAction (e não Restrict/NoAction): com o histórico required já
        // rastreado, Restrict/NoAction lançam "required relationship severed" ao
        // marcar a Unidade como Deleted — ANTES de o interceptor convertê-la em
        // soft-delete. ClientNoAction instrui o EF a não tocar nem validar os
        // dependentes; o interceptor então converte a Unidade em UPDATE (soft-delete)
        // e o histórico permanece intacto. A Unidade nunca é hard-deletada, logo a
        // integridade referencial é preservada na prática (FK NO ACTION no banco).
        builder.HasMany(u => u.Historico)
            .WithOne()
            .HasForeignKey(h => h.UnidadeId)
            .OnDelete(DeleteBehavior.ClientNoAction);
        builder.Navigation(u => u.Historico)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Índices únicos parciais (WHERE is_deleted = false) — unicidade entre vivos
        builder.HasIndex(u => u.Slug)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_unidade_slug_vivo");

        builder.HasIndex(u => u.Sigla)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_unidade_sigla_vivo");

        builder.HasIndex(u => u.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_unidade_codigo_vivo");

        // Alias: índice não-único (para agrupamento/busca)
        builder.HasIndex(u => u.Alias)
            .HasDatabaseName("ix_unidade_alias");

        // Hierarquia: índice para busca de subordinadas
        builder.HasIndex(u => u.UnidadeSuperiorId)
            .HasDatabaseName("ix_unidade_superior_id");
    }
}
