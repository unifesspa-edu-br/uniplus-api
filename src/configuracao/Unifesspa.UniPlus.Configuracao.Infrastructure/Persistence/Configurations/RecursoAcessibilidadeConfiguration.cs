namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class RecursoAcessibilidadeConfiguration
    : IEntityTypeConfiguration<RecursoAcessibilidade>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<RecursoAcessibilidade> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "recurso_acessibilidade",
            t =>
            {
                // Formato fechado do código (UPPER_SNAKE iniciando por letra) — defesa
                // em profundidade do invariante de domínio (CodigoRecursoAcessibilidade)
                // contra inserts crus. Case-sensitive, alinhado ao value object.
                t.HasCheckConstraint(
                    "ck_recurso_acessibilidade_codigo_formato",
                    "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
            });

        builder.HasKey(r => r.Id);

        // Codigo é value object — persistido por valor como varchar via
        // CodigoRecursoAcessibilidadeValueConverter (reidratação fail-fast). O nome de
        // coluna snake_case vem da convenção global; o CHECK acima restringe o formato.
        builder.Property(r => r.Codigo)
            .HasConversion<CodigoRecursoAcessibilidadeValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

        builder.Property(r => r.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(r => r.Descricao).HasMaxLength(DescricaoMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(r => r.CreatedBy).HasMaxLength(255);
        builder.Property(r => r.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre recursos vivos (índice parcial) — um recurso vivo
        // por código; soft-delete libera o slot para recriação.
        builder.HasIndex(r => r.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_recurso_acessibilidade_codigo_vivo");

        // Unicidade do nome entre recursos vivos (índice parcial) — um recurso vivo
        // por nome; soft-delete libera o slot para recriação.
        builder.HasIndex(r => r.Nome)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_recurso_acessibilidade_nome_vivo");
    }
}
