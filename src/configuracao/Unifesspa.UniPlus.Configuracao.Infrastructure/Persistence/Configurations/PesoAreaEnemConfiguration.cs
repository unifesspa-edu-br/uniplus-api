namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

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

    public void Configure(EntityTypeBuilder<PesoAreaEnem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "peso_area_enem",
            t =>
            {
                // Domínio fechado do grupo de área (Res. 805/2024, Anexo I).
                t.HasCheckConstraint(
                    "ck_peso_area_enem_grupo_curso",
                    "grupo_curso IN ('Tecnológica', 'Humanística I', 'Humanística II', 'Saúde e Biológicas')");

                // Não-negatividade dos cinco pesos e do corte de redação.
                t.HasCheckConstraint("ck_peso_area_enem_peso_redacao", "peso_redacao >= 0");
                t.HasCheckConstraint("ck_peso_area_enem_peso_ciencias_natureza", "peso_ciencias_natureza >= 0");
                t.HasCheckConstraint("ck_peso_area_enem_peso_ciencias_humanas", "peso_ciencias_humanas >= 0");
                t.HasCheckConstraint("ck_peso_area_enem_peso_linguagens", "peso_linguagens >= 0");
                t.HasCheckConstraint("ck_peso_area_enem_peso_matematica", "peso_matematica >= 0");

                // Corte de redação na faixa válida da nota do ENEM (0–1000). O teto
                // também impede o overflow da coluna numeric(7,3) por insert fora do app.
                t.HasCheckConstraint("ck_peso_area_enem_corte_redacao", "corte_redacao >= 0 AND corte_redacao <= 1000");
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

        // Cinco pesos numeric(4,2) e corte de redação numeric(7,3) — escala 3 com 4
        // dígitos inteiros para acomodar a nota máxima da redação do ENEM (1000),
        // que não cabe em numeric(6,3). DEFAULT 400 no banco (Anexo I) para inserts
        // fora do fluxo da aplicação; a factory do agregado já resolve o padrão
        // quando o corte é omitido no command.
        builder.Property(p => p.PesoRedacao).HasPrecision(4, 2).IsRequired();
        builder.Property(p => p.PesoCienciasNatureza).HasPrecision(4, 2).IsRequired();
        builder.Property(p => p.PesoCienciasHumanas).HasPrecision(4, 2).IsRequired();
        builder.Property(p => p.PesoLinguagens).HasPrecision(4, 2).IsRequired();
        builder.Property(p => p.PesoMatematica).HasPrecision(4, 2).IsRequired();
        builder.Property(p => p.CorteRedacao)
            .HasPrecision(7, 3)
            .HasDefaultValue(PesoAreaEnem.CorteRedacaoPadrao)
            .IsRequired();

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
