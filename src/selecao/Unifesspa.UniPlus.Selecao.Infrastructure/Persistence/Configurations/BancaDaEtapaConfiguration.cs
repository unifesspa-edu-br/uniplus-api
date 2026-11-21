namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Mapeamento de <see cref="BancaDaEtapa"/> — filha de <see cref="EtapaProcesso"/>.</summary>
public sealed class BancaDaEtapaConfiguration : IEntityTypeConfiguration<BancaDaEtapa>
{
    public void Configure(EntityTypeBuilder<BancaDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bancas_da_etapa");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Codigo).HasMaxLength(60).IsRequired();
        builder.Property(b => b.Nome).HasMaxLength(200).IsRequired();
        builder.OwnsOne(b => b.RegraDeDeliberacao, regra => regra.ConfigurarRegraDeDeliberacao());
        builder.Property(b => b.TipoBancaOrigemId).IsRequired();
        builder.Property(b => b.PosicaoNoTipo).IsRequired();

        // A unicidade do par código e posição é a restrição adiada do script de posição única das bancas.
        builder.HasIndex(b => new { b.EtapaProcessoId, b.Codigo })
            .HasDatabaseName("ix_bancas_da_etapa_codigo");
    }
}
