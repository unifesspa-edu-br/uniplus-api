namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Configuração EF Core de <see cref="BancaRequerida"/> (Story #851) — filha 0..* de <see cref="FaseCronograma"/>.</summary>
public sealed class BancaRequeridaConfiguration : IEntityTypeConfiguration<BancaRequerida>
{
    public void Configure(EntityTypeBuilder<BancaRequerida> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bancas_requeridas");
        builder.HasKey(b => b.Id);
        builder.Property<Guid>("ProcessoSeletivoId").HasComment("Processo seletivo ao qual a banca pertence.");
        builder.Property(b => b.Chave)
            .HasConversion(chave => chave.Valor, valor => ChaveLocal.Criar(valor).Value)
            .HasColumnName("chave_local").HasMaxLength(ChaveLocal.ComprimentoMaximo).IsRequired()
            .HasComment("Identificador que a parte guarda dentro da configuração publicada; único no processo.");
        builder.HasIndex("ProcessoSeletivoId", nameof(BancaRequerida.Chave)).IsUnique().HasDatabaseName("ux_bancas_requeridas_processo_chave_local");
        builder.Property(b => b.Id).ValueGeneratedNever().HasComment("Identificador da banca.");
        builder.Property(b => b.FaseCronogramaId).HasComment("Identificador da fase à qual a banca pertence.");

        builder.OwnsOne(b => b.Tipo, tipo => tipo.ConfigurarTipoDeBancaCopiado());
        builder.Navigation(b => b.Tipo).IsRequired();
        builder.Property(b => b.PosicaoNoTipo).IsRequired().HasComment("Posição da banca entre as bancas do mesmo tipo.");
        builder.OwnsOne(b => b.RegraDeDeliberacao, regra => regra.ConfigurarRegraDeDeliberacao());

        builder.HasMany(b => b.CandidatosAtendidos)
            .WithOne()
            .HasForeignKey(c => c.BancaRequeridaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.CandidatosAtendidos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
