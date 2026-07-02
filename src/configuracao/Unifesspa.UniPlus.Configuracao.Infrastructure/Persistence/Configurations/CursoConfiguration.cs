namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class CursoConfiguration
    : IEntityTypeConfiguration<Curso>
{
    private const int CodigoMaxLength = 60;
    private const int NomeMaxLength = 200;
    private const int GrauMaxLength = 60;
    private const int NivelEnsinoMaxLength = 60;
    private const int GrupoAreaEnemMaxLength = 30;

    public void Configure(EntityTypeBuilder<Curso> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "curso",
            t =>
            {
                // Domínio fechado do grupo de área do ENEM (Res. 805/2024, Anexo I) —
                // espelha o CHECK de peso_area_enem, mas null-safe: a coluna é opcional
                // (nem todo curso classifica por área do ENEM).
                t.HasCheckConstraint(
                    "ck_curso_grupo_area_enem",
                    "grupo_area_enem IS NULL OR grupo_area_enem IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
            });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Codigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(c => c.Nome).HasMaxLength(NomeMaxLength).IsRequired();
        builder.Property(c => c.Grau).HasMaxLength(GrauMaxLength).IsRequired();
        builder.Property(c => c.NivelEnsino).HasMaxLength(NivelEnsinoMaxLength).IsRequired();

        // GrupoAreaEnem é value object opcional — persistido por valor como varchar
        // via GrupoCursoValueConverter (reidratação fail-fast; o converter só é
        // aplicado a valores não-nulos). O CHECK acima restringe a coluna ao
        // domínio fechado. O nome de coluna snake_case vem da convenção global.
        builder.Property(c => c.GrupoAreaEnem)
            .HasConversion<GrupoCursoValueConverter>()
            .HasMaxLength(GrupoAreaEnemMaxLength);

        // Auditoria (IAuditableEntity)
        builder.Property(c => c.CreatedBy).HasMaxLength(255);
        builder.Property(c => c.UpdatedBy).HasMaxLength(255);

        // Unicidade do código entre cursos vivos (índice parcial) — um curso vivo
        // por código; soft-delete libera o slot para recriação.
        builder.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_curso_codigo_vivo");
    }
}
