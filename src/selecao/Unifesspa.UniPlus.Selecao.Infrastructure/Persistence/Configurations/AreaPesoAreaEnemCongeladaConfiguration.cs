namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Configuração EF Core de <see cref="AreaPesoAreaEnemCongelada"/> — uma área de um grupo
/// do quadro de pesos por área congelado na classificação.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class AreaPesoAreaEnemCongeladaConfiguration : IEntityTypeConfiguration<AreaPesoAreaEnemCongelada>
{
    public void Configure(EntityTypeBuilder<AreaPesoAreaEnemCongelada> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("areas_peso_area_enem_congeladas", t =>
        {
            t.HasComment(
                "Peso e corte de cada área do ENEM num grupo do quadro de pesos por área congelado na " +
                "classificação, copiados por valor da resolução de Pesos por Área declarada.");

            // As mesmas faixas da tabela de origem, no cadastro de Pesos por Área: gravação que
            // não passe pelo agregado não deixa peso negativo nem corte fora da nota do ENEM.
            t.HasCheckConstraint("ck_areas_peso_area_enem_congeladas_peso", "peso >= 0");
            t.HasCheckConstraint(
                "ck_areas_peso_area_enem_congeladas_corte",
                $"corte IS NULL OR (corte >= 0 AND corte <= {GrupoPesoAreaEnemCongelado.CorteMaximo.ToString(CultureInfo.InvariantCulture)})");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedNever()
            .HasComment("Identificador interno (UUIDv7) da área congelada.");

        builder.Property(a => a.GrupoPesoAreaEnemCongeladoId)
            .HasComment("Id do grupo congelado dono da área (FK, cascade delete).");

        builder.Property(a => a.CreatedAt)
            .HasComment("Instante de criação do registro (auditoria, carimbado pelo AuditableInterceptor).");
        builder.Property(a => a.UpdatedAt)
            .HasComment("Instante da última atualização do registro (auditoria, carimbado pelo AuditableInterceptor).");

        builder.Property(a => a.Codigo)
            .HasMaxLength(LimitesDoEnvelope.AreaPesoAreaEnemCodigo)
            .IsRequired()
            .HasComment("Código da área do ENEM, sem abreviação e sem acento, copiado da resolução de Pesos por Área.");
        builder.Property(a => a.Rotulo)
            .HasMaxLength(LimitesDoEnvelope.AreaPesoAreaEnemRotulo)
            .IsRequired()
            .HasComment("Rótulo oficial da área do ENEM, copiado junto do código.");
        builder.Property(a => a.Peso)
            .HasPrecision(LimitesDoEnvelope.PrecisaoPesoAreaEnem, 4)
            .IsRequired()
            .HasComment("Peso da área na média do grupo, copiado da resolução de Pesos por Área.");
        builder.Property(a => a.Corte)
            .HasPrecision(LimitesDoEnvelope.PrecisaoCorteAreaEnem, 4)
            .HasComment("Nota mínima da área (0 a 1000), copiada da resolução de Pesos por Área; nulo quando a área não tem corte.");

        builder.HasIndex(a => new { a.GrupoPesoAreaEnemCongeladoId, a.Codigo })
            .IsUnique();
    }
}
