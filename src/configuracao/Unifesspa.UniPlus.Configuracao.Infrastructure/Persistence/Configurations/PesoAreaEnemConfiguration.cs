namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Converters;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class PesoAreaEnemConfiguration
    : IEntityTypeConfiguration<PesoAreaEnem>
{
    private const int ResolucaoMaxLength = 40;
    private const int GrupoCursoMaxLength = 30;
    private const int BaseLegalMaxLength = 500;
    private const int AreaCodigoMaxLength = 30;
    private const int AreaRotuloMaxLength = 100;

    public void Configure(EntityTypeBuilder<PesoAreaEnem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "peso_area_enem",
            t =>
            {
                // Domínio fechado do grupo de área (Res. nº 805/2024/Consepe, Anexo I).
                t.HasCheckConstraint(
                    "ck_peso_area_enem_grupo_curso",
                    "grupo_curso IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");
            });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Resolucao)
            .HasMaxLength(ResolucaoMaxLength)
            .IsRequired();

        // GrupoCurso é value object — persistido por valor como varchar via
        // GrupoCursoValueConverter (reidratação fail-fast). O nome de coluna
        // snake_case vem da convenção global.
        builder.Property(p => p.GrupoCurso)
            .HasConversion<GrupoCursoValueConverter>()
            .HasMaxLength(GrupoCursoMaxLength)
            .IsRequired();

        // As cinco áreas numa tabela filha, uma linha por área. A chave (pai, código)
        // garante cada área uma vez por linha de pesos; código e rótulo são postos pelo
        // sistema a partir de PesoAreaEnem.Areas, de onde vem também o CHECK do código.
        // O agregado expõe as áreas ordenadas; o EF lê e grava o campo.
        builder.OwnsMany(p => p.AreasDaLinha, area =>
        {
            area.ToTable(
                "peso_area_enem_area",
                t =>
                {
                    t.HasComment(
                        "Peso e corte de cada uma das cinco áreas do ENEM numa linha de Pesos por Área. "
                        + "Código e rótulo são postos pelo sistema; o operador edita só peso e corte.");

                    t.HasCheckConstraint(
                        "ck_peso_area_enem_area_codigo",
                        $"codigo IN ({string.Join(", ", PesoAreaEnem.Areas.Select(static a => $"'{a.Codigo}'"))})");
                    t.HasCheckConstraint("ck_peso_area_enem_area_peso", "peso >= 0");

                    // Corte opcional na faixa da nota do ENEM (0–1000). O teto também
                    // impede o overflow da coluna numeric(7,3) por insert fora do app.
                    t.HasCheckConstraint(
                        "ck_peso_area_enem_area_corte",
                        $"corte IS NULL OR (corte >= 0 AND corte <= {PesoAreaEnem.CorteMaximo.ToString(CultureInfo.InvariantCulture)})");
                });

            area.WithOwner().HasForeignKey("PesoAreaEnemId");
            area.HasKey("PesoAreaEnemId", nameof(PesoAreaEnemArea.Codigo));

            area.Property<Guid>("PesoAreaEnemId")
                .HasComment("Linha de Pesos por Área (resolução e grupo de área) a que a área pertence.");

            area.Property(a => a.Codigo)
                .HasMaxLength(AreaCodigoMaxLength)
                .IsRequired()
                .HasComment("Código da área do ENEM, sem abreviação e sem acento (ex.: REDACAO); identidade estável.");
            area.Property(a => a.Rotulo)
                .HasMaxLength(AreaRotuloMaxLength)
                .IsRequired()
                .HasComment("Rótulo oficial da área (Anexo I da Resolução nº 805/2024/Consepe), posto pelo sistema a partir do código.");

            // Peso numeric(4,2); corte numeric(7,3) — escala 3 com 4 dígitos inteiros para
            // acomodar a nota máxima de uma área do ENEM (1000), que não cabe em numeric(6,3).
            area.Property(a => a.Peso)
                .HasPrecision(4, 2)
                .IsRequired()
                .HasComment("Peso da área na média ponderada da nota do ENEM: multiplicador adimensional, de 0 a 99,99.");
            area.Property(a => a.Corte)
                .HasPrecision(7, 3)
                .HasComment("Nota mínima da área na escala do ENEM (0 a 1000). Nulo quando a área não tem corte.");
        });

        builder.Navigation(p => p.AreasDaLinha)
            .HasField("_areas")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(p => p.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();

        // Auditoria (IAuditableEntity)
        builder.Property(p => p.CreatedBy).HasMaxLength(255);
        builder.Property(p => p.UpdatedBy).HasMaxLength(255);

        // Unicidade do par (resolução, grupo de curso) entre linhas vivas (índice
        // parcial) — uma linha por par; soft-delete libera o slot para recriação.
        builder.HasIndex(p => new { p.Resolucao, p.GrupoCurso })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_peso_area_enem_resolucao_grupo_vivo");
    }
}
