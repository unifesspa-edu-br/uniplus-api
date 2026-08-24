namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class TipoDeficienciaConfiguration
    : IEntityTypeConfiguration<TipoDeficiencia>
{
    private const int CodigoMaxLength = 50;
    private const int NomeMaxLength = 200;
    private const int DescricaoMaxLength = 1000;

    public void Configure(EntityTypeBuilder<TipoDeficiencia> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "tipo_deficiencia",
            t =>
            {
                // Formato fechado do código (UPPER_SNAKE iniciando por letra) — defesa
                // em profundidade do invariante de domínio (CodigoTipoDeficiencia)
                // contra inserts crus. Case-sensitive, alinhado ao value object.
                t.HasCheckConstraint(
                    "ck_tipo_deficiencia_codigo_formato",
                    "codigo ~ '^[A-Z][A-Z0-9_]{1,49}$'");
            });

        builder.HasKey(t => t.Id);

        // Codigo é value object — persistido por valor como varchar via
        // CodigoTipoDeficienciaValueConverter (reidratação fail-fast). O nome de
        // coluna snake_case vem da convenção global; o CHECK acima restringe o formato.
        builder.Property(t => t.Codigo)
            .HasConversion<CodigoTipoDeficienciaValueConverter>()
            .HasMaxLength(CodigoMaxLength)
            .IsRequired();

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

        // Unicidade do código entre tipos vivos (índice parcial) — um tipo vivo por
        // código; soft-delete libera o slot para recriação.
        builder.HasIndex(t => t.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_deficiencia_codigo_vivo");

        // Unicidade do nome entre tipos vivos (índice parcial) — um tipo vivo por
        // nome; soft-delete libera o slot para recriação.
        builder.HasIndex(t => t.Nome)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_tipo_deficiencia_nome_vivo");
    }
}
