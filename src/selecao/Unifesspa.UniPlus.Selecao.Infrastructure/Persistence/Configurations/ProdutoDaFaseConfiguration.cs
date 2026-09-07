namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Configuração EF Core de <see cref="ProdutoDaFase"/> — filha 0..* de
/// <see cref="FaseCronograma"/>. Os limites de comprimento espelham
/// <c>LimitesDoEnvelope</c> (constantes duplicadas por convenção do repo, verificadas
/// pelo fitness <c>LimitesDoEnvelopeBatemComOSchemaTests</c>).
/// </summary>
public sealed class ProdutoDaFaseConfiguration : IEntityTypeConfiguration<ProdutoDaFase>
{
    private const int AtoCodigoMaxLength = 60;

    public void Configure(EntityTypeBuilder<ProdutoDaFase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("produtos_da_fase");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.AtoCodigo).HasMaxLength(AtoCodigoMaxLength).IsRequired();
        builder.Property(p => p.Papel).HasConversion<int>();

        // O mesmo tipo de ato declarado uma única vez por fase. É a chave de reconciliação
        // de FaseCronograma.AtualizarSnapshot e a chave de round-trip do envelope — a
        // rejeição no domínio é check-then-act não-atômico, e esta constraint é a defesa
        // atômica.
        builder.HasIndex(p => new { p.FaseCronogramaId, p.AtoCodigo })
            .IsUnique()
            .HasDatabaseName("ux_produtos_da_fase_ato");
    }
}
