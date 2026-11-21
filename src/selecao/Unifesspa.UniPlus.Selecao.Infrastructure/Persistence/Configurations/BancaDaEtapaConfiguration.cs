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
        builder.OwnsOne(b => b.Tipo, tipo => tipo.ConfigurarTipoDeBancaCopiado());
        builder.Navigation(b => b.Tipo).IsRequired();
        builder.OwnsOne(b => b.RegraDeDeliberacao, regra => regra.ConfigurarRegraDeDeliberacao());
        builder.Property(b => b.PosicaoNoTipo).IsRequired();

        // A unicidade do par código e posição, e o índice que atende à busca por lugar e tipo, são a restrição adiada do
        // script de posição única das bancas.
    }
}
