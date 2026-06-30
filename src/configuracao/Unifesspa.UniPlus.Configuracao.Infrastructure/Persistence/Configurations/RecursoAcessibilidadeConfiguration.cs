namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RecursoAcessibilidadeConfiguration
    : IEntityTypeConfiguration<RecursoAcessibilidade>
{
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<RecursoAcessibilidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("recurso_acessibilidade");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(r => r.Descricao).HasMaxLength(DescricaoMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(r => r.CreatedBy).HasMaxLength(255);
        builder.Property(r => r.UpdatedBy).HasMaxLength(255);

        // Unicidade do nome entre recursos vivos (índice parcial) — um recurso vivo
        // por nome; soft-delete libera o slot para recriação.
        builder.HasIndex(r => r.Nome)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_recurso_acessibilidade_nome_vivo");
    }
}
