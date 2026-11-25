namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

/// <summary>Mapeamento de <see cref="ItemDeAvaliacao"/> — filha de <see cref="EtapaProcesso"/>.</summary>
public sealed class ItemDeAvaliacaoConfiguration : IEntityTypeConfiguration<ItemDeAvaliacao>
{
    public void Configure(EntityTypeBuilder<ItemDeAvaliacao> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ClassificaPorPadrao(ClassificacaoDeProtecao.Interno);

        // Só a positividade fica no banco: que a soma dos itens feche a nota máxima da etapa e
        // que a pontuação caiba no passo dependem de outra linha e do rascunho, e são regra do
        // domínio e da conferência de publicação.
        builder.ToTable("itens_de_avaliacao_da_etapa", t =>
        {
            t.HasComment("Itens de avaliação (critérios ou blocos) da etapa; o id é a identidade a que lançamento e recurso apontam.");
            t.HasCheckConstraint("ck_itens_de_avaliacao_da_etapa_pontuacao_positiva", "pontuacao_maxima > 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever().HasComment("Identificador do item de avaliação.");
        builder.Property(i => i.Chave)
            .HasConversion<ChaveLocalValueConverter>()
            .HasColumnName("chave_local").HasMaxLength(ChaveLocal.ComprimentoMaximo).IsRequired().HasComment("Identificador que a parte guarda dentro da configuração publicada; único na etapa.");
        builder.HasIndex(i => new { i.EtapaProcessoId, i.Chave }).IsUnique().HasDatabaseName("ux_itens_de_avaliacao_da_etapa_chave_local");
        builder.Property(i => i.EtapaProcessoId).HasComment("Etapa à qual o item pertence.");
        // A unicidade da ordem é adiada pelo script de restrições para permitir reordenação.
        builder.Property(i => i.Ordem).IsRequired().HasComment("Posição do item na etapa, a partir de um.");
        builder.Property(i => i.Rotulo).HasMaxLength(ItemDeAvaliacao.RotuloMaxLength).IsRequired().HasComment("Rótulo do critério de avaliação.");
        builder.Property(i => i.PontuacaoMaxima).HasPrecision(18, 4)
            .HasComment("O quanto o item vale da nota máxima da etapa.");

        builder.HasIndex(i => i.EtapaProcessoId);
    }
}
