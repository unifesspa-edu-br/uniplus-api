namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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

        // Trio de cidade all-or-nothing (issue #1114): espelha no banco a invariante
        // de domínio já provada por Unidade.ValidarReferenciaCidade — mesmo padrão de
        // InstituicaoConfiguration.
        builder.ToTable("unidade", t => t.HasCheckConstraint(
            "ck_unidade_cidade_completa",
            "(cidade_codigo_ibge IS NULL AND cidade_nome IS NULL AND cidade_uf IS NULL) "
            + "OR (cidade_codigo_ibge IS NOT NULL AND cidade_nome IS NOT NULL AND cidade_uf IS NOT NULL)"));
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

        builder.Property(u => u.UnidadeAcademica).IsRequired();
        builder.Property(u => u.VigenciaInicio).IsRequired();
        builder.Property(u => u.VigenciaFim);

        // Referência de cidade do Geo (ADR-0090, issue #1114) — código + display
        // cache, opcional all-or-nothing, sem FK cross-banco para uniplus_geo.
        builder.Property(u => u.CidadeCodigoIbge)
            .HasMaxLength(ReferenciaCidadeGeo.CodigoIbgeLength)
            .IsFixedLength()
            .HasComment("Código IBGE (7 dígitos) da cidade da Unidade — referência ao Geo, sem FK cross-banco; opcional all-or-nothing com nome/UF.");
        builder.Property(u => u.CidadeNome)
            .HasMaxLength(ReferenciaCidadeGeo.NomeMaxLength)
            .HasComment("Nome de exibição da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização.");
        builder.Property(u => u.CidadeUf)
            .HasMaxLength(ReferenciaCidadeGeo.UfLength)
            .IsFixedLength()
            .HasComment("UF da cidade (display cache) — snapshot do Geo no momento do cadastro/atualização.");

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
