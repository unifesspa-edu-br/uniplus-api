namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Mapeamento de <see cref="ProdutoDaEtapa"/> — filha de <see cref="EtapaProcesso"/>, no
/// mesmo desenho de <see cref="ProdutoDaFaseConfiguration"/> um nível abaixo.
/// </summary>
public sealed class ProdutoDaEtapaConfiguration : IEntityTypeConfiguration<ProdutoDaEtapa>
{
    private const int AtoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<ProdutoDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("produtos_da_etapa");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.AtoCodigo).HasMaxLength(AtoCodigoMaxLength).IsRequired();
        builder.Property(p => p.Papel).HasConversion<int>();

        // O mesmo tipo de ato uma única vez por etapa: a rejeição no domínio é
        // check-then-act, e esta constraint é a defesa atômica.
        builder.HasIndex(p => new { p.EtapaProcessoId, p.AtoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_produtos_da_etapa_ato");
    }
}
