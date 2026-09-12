namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Mapeamento de <see cref="BancaDaEtapa"/> — filha de <see cref="EtapaProcesso"/>.</summary>
public sealed class BancaDaEtapaConfiguration : IEntityTypeConfiguration<BancaDaEtapa>
{
    public void Configure(EntityTypeBuilder<BancaDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("bancas_da_etapa");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Codigo).HasMaxLength(60).IsRequired();
        builder.Property(b => b.TipoBancaOrigemId).IsRequired();

        // O mesmo tipo de banca uma única vez por etapa — defesa atômica da guarda de domínio.
        builder.HasIndex(b => new { b.EtapaProcessoId, b.Codigo })
            .IsUnique()
            .HasDatabaseName("ux_bancas_da_etapa_codigo");
    }
}
