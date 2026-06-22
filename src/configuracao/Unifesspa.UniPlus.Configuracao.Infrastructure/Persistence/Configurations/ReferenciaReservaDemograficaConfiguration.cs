namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class ReferenciaReservaDemograficaConfiguration
    : IEntityTypeConfiguration<ReferenciaReservaDemografica>
{
    private const int CensoReferenciaMaxLength = 20;
    private const int BaseLegalMaxLength = 500;

    public void Configure(EntityTypeBuilder<ReferenciaReservaDemografica> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "referencia_reserva_demografica",
            t =>
            {
                t.HasCheckConstraint(
                    "ck_referencia_reserva_demografica_ppi_percentual",
                    "ppi_percentual >= 0 AND ppi_percentual <= 100");
                t.HasCheckConstraint(
                    "ck_referencia_reserva_demografica_quilombola_percentual",
                    "quilombola_percentual >= 0 AND quilombola_percentual <= 100");
                t.HasCheckConstraint(
                    "ck_referencia_reserva_demografica_pcd_percentual",
                    "pcd_percentual >= 0 AND pcd_percentual <= 100");
            });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CensoReferencia)
            .HasMaxLength(CensoReferenciaMaxLength)
            .IsRequired();

        // Percentual é value object — persistido por valor como numeric(5,2).
        // A reconstrução confia no CHECK de banco (0–100), por isso usa Value!.
        // Os nomes de coluna (ppi_percentual, ...) vêm da convenção snake_case
        // global — alinhado ao CampusConfiguration, sem HasColumnName explícito.
        builder.Property(r => r.PpiPercentual)
            .HasConversion(p => p.Valor, v => Percentual.Criar(v).Value!)
            .HasPrecision(5, 2)
            .IsRequired();
        builder.Property(r => r.QuilombolaPercentual)
            .HasConversion(p => p.Valor, v => Percentual.Criar(v).Value!)
            .HasPrecision(5, 2)
            .IsRequired();
        builder.Property(r => r.PcdPercentual)
            .HasConversion(p => p.Valor, v => Percentual.Criar(v).Value!)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(r => r.BaseLegal).HasMaxLength(BaseLegalMaxLength).IsRequired();

        // Auditoria (IAuditableEntity)
        builder.Property(r => r.CreatedBy).HasMaxLength(255);
        builder.Property(r => r.UpdatedBy).HasMaxLength(255);

        // Unicidade do Censo entre referências vivas (índice parcial) — uma linha
        // por Censo; soft-delete libera o slot para recriação.
        builder.HasIndex(r => r.CensoReferencia)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_referencia_reserva_demografica_censo_vivo");
    }
}
