namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Configuração EF Core de <see cref="DestinoRemanejamento"/> (Story #575) —
/// entidade filha de <see cref="ConfiguracaoCascataRemanejamento"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via EF Core ModelBuilder.ApplyConfigurationsFromAssembly por reflection.")]
internal sealed class DestinoRemanejamentoConfiguration : IEntityTypeConfiguration<DestinoRemanejamento>
{
    private const int CodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<DestinoRemanejamento> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("destinos_remanejamento");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.ModalidadeOrigemCodigo).HasMaxLength(CodigoMaxLength).IsRequired();
        builder.Property(d => d.Ordem).IsRequired();
        builder.Property(d => d.ModalidadeDestinoCodigo).HasMaxLength(CodigoMaxLength).IsRequired();

        builder.HasIndex(d => new { d.ConfiguracaoCascataRemanejamentoId, d.ModalidadeOrigemCodigo, d.Ordem })
            .IsUnique()
            .HasDatabaseName("ux_destinos_remanejamento_cascata_origem_ordem");

        builder.HasIndex(d => new { d.ConfiguracaoCascataRemanejamentoId, d.ModalidadeOrigemCodigo, d.ModalidadeDestinoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_destinos_remanejamento_cascata_origem_destino");

        builder.ToTable(t => t.HasCheckConstraint("ck_destinos_remanejamento_ordem_positiva", "ordem >= 1"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_destinos_remanejamento_origem_diferente_destino",
            "modalidade_origem_codigo <> modalidade_destino_codigo"));
    }
}
