namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TipoDeficienciaConfiguration
    : IEntityTypeConfiguration<TipoDeficiencia>
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<TipoDeficiencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tipo_deficiencia");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nome).HasMaxLength(NomeMaxLength).IsRequired();

        // Descrição obrigatória (ADR-0116): serve também como a descrição por
        // valor do fato TIPO_DEFICIENCIA (DECLARADO).
        builder.Property(t => t.Descricao).HasMaxLength(DescricaoMaxLength).IsRequired();

        // Permanente (ADR-0116): nullable — null = ainda não classificado pelo
        // CEPS (task 0.1, taxonomia residual), distinto de false = classificado
        // como não-permanente.
        builder.Property(t => t.Permanente);

        // Auditoria (IAuditableEntity)
        builder.Property(t => t.CreatedBy).HasMaxLength(255);
        builder.Property(t => t.UpdatedBy).HasMaxLength(255);

        // Unicidade do nome entre tipos vivos (índice parcial) — um tipo vivo por
        // nome; soft-delete libera o slot para recriação.
        builder.HasIndex(t => t.Nome)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_deficiencia_nome_vivo");
    }
}
