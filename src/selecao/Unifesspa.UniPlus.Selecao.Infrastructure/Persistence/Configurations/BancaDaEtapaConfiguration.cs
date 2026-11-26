namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

/// <summary>Mapeamento de <see cref="BancaDaEtapa"/> — filha de <see cref="EtapaProcesso"/>.</summary>
public sealed class BancaDaEtapaConfiguration : IEntityTypeConfiguration<BancaDaEtapa>
{
    public void Configure(EntityTypeBuilder<BancaDaEtapa> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        builder.ToTable("bancas_da_etapa", tabela => tabela.HasCheckConstraint("ck_bancas_da_etapa_consequencia_da_ausencia",
            $"consequencia_da_ausencia IS NULL OR consequencia_da_ausencia IN ({SqlDosCodigosDeContrato.Lista<ConsequenciaDoDescumprimento>()})"));
        builder.Property(b => b.ConsequenciaDaAusencia).HasMaxLength(30)
            .ClassificaComo(ClassificacaoDeProtecao.Publico)
            .HasComment("Consequência própria da ausência do candidato à banca.");
        builder.HasKey(b => b.Id);
        builder.Property<Guid>("ProcessoSeletivoId").HasComment("Processo seletivo ao qual a banca pertence.");
        builder.Property(b => b.Chave)
            .HasConversion<ChaveLocalValueConverter>()
            .HasColumnName("chave_local").HasMaxLength(ChaveLocal.ComprimentoMaximo).IsRequired()
            .HasComment("Identificador que a parte guarda dentro da configuração publicada; único no processo.");
        builder.HasIndex("ProcessoSeletivoId", nameof(BancaDaEtapa.Chave)).IsUnique().HasDatabaseName("ux_bancas_da_etapa_processo_chave_local");
        builder.Property(b => b.Id).ValueGeneratedNever().HasComment("Identificador da banca.");
        builder.Property(b => b.EtapaProcessoId).HasComment("Identificador da etapa à qual a banca pertence.");
        builder.OwnsOne(b => b.Tipo, tipo => tipo.ConfigurarTipoDeBancaCopiado());
        builder.Navigation(b => b.Tipo).IsRequired();
        builder.OwnsOne(b => b.RegraDeDeliberacao, regra => regra.ConfigurarRegraDeDeliberacao());
        builder.Property(b => b.PosicaoNoTipo).IsRequired().HasComment("Posição da banca entre as bancas do mesmo tipo.");

        // A unicidade do par código e posição, e o índice que atende à busca por lugar e tipo, são a restrição adiada do
        // script de posição única das bancas.
    }
}
